// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Public dispatch helper for the Publish interceptor. Uses
    /// <see cref="NotificationHandlerCache{TNotification}"/> to resolve handlers once per
    /// scope, then dispatches sequentially with sync fast-path.
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationCachedDispatcher
    {
        /// <summary>
        /// Resolves handlers via ThreadStatic cache and dispatches sequentially.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task DispatchSequential<TNotification>(
            TNotification notification,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            var factories = NotificationDispatch<TNotification>.Handlers;

            if (factories == null || factories.Length == 0)
                return Task.CompletedTask;

            var handlers = NotificationHandlerCache<TNotification>.Resolve(serviceProvider, factories);
            return DispatchSequential(handlers, notification, cancellationToken);
        }

        /// <summary>
        /// Dispatches an ALREADY-RESOLVED handler array sequentially.
        /// <para>
        /// The generated fast path resolves the array itself so it can exact-type-verify the handlers,
        /// and falls back here when verification fails. It must hand the array over rather than let
        /// this method resolve again: when the array is not reusable — any handler registered Transient
        /// — a second resolve CONSTRUCTS every handler a second time, so one Publish ran each handler's
        /// constructor twice.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task DispatchSequential<TNotification>(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            // Sync fast-path: if all handlers complete synchronously, avoid async state machine.
            for (int i = 0; i < handlers.Length; i++)
            {
                var task = handlers[i].Handle(notification, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    return DispatchRemainingAsync(handlers, notification, i, task, cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// The single observed entry. Every route funnels through here, which is what makes "exactly
        /// one observation per publish" true by construction rather than by remembering to probe the
        /// right number of places — the previous design probed three sites that reach each other and
        /// would have opened two or three observations for one publish.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Task DispatchRouted<TNotification>(
            TNotification notification,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            var observer = MediatorObservation.ResolveNotificationObserver(serviceProvider);

            // Registered in some container, absent or idle in this one: nothing to observe, and the
            // ordinary dispatch is reached without having built anything.
            if (observer is null || !observer.IsActive)
                return DispatchSequential(notification, serviceProvider, cancellationToken);

            var scope = observer.BeginPublish(notification);
            if (scope is null)
                return DispatchSequential(notification, serviceProvider, cancellationToken);

            return DispatchObserved(scope, notification, serviceProvider, cancellationToken);
        }

        /// <summary>
        /// The observed twin. It is <c>async</c> deliberately, and that is load-bearing rather than
        /// stylistic: the async method builder saves and restores the caller's ExecutionContext
        /// across the synchronous-return boundary, so whatever ambient state an observer sets while
        /// a subscriber runs cannot escape into the code that called Publish. A synchronous body
        /// observing the same way leaks it, measured.
        /// <para>
        /// It also awaits every handler rather than taking the completed-task shortcut. A subscriber
        /// observation has to stay open for the handler's WHOLE task — closing it when Handle returns
        /// reports an asynchronous subscriber as having taken no time — and this path is cold by
        /// construction, so the shortcut buys nothing here.
        /// </para>
        /// </summary>
        private static async Task DispatchObserved<TNotification>(
            IMediatorPublishScope scope,
            TNotification notification,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            try
            {
                var factories = NotificationDispatch<TNotification>.Handlers;
                if (factories is null || factories.Length == 0)
                    return;

                var handlers = NotificationHandlerCache<TNotification>.Resolve(serviceProvider, factories);

                for (int i = 0; i < handlers.Length; i++)
                {
                    var handler = handlers[i];
                    var subscriber = scope.BeginSubscriber(handler);

                    try
                    {
                        await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Per subscriber, not per publish: one handler failing is not the publish
                        // failing, and an adapter has to be able to mark the one.
                        subscriber?.OnError(ex);
                        throw;
                    }
                    finally
                    {
                        subscriber?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                scope.OnError(ex);
                throw;
            }
            finally
            {
                // In a finally so a handler that throws SYNCHRONOUSLY cannot leave the observation
                // open. An exporter that fires on close would otherwise drop the whole publish.
                scope.Dispose();
            }
        }

        private static async Task DispatchRemainingAsync<TNotification>(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            int currentIndex,
            Task pendingTask,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            await pendingTask.ConfigureAwait(false);

            for (int i = currentIndex + 1; i < handlers.Length; i++)
            {
                var task = handlers[i].Handle(notification, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    await task.ConfigureAwait(false);
            }
        }
    }
}
