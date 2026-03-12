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

            // Sync fast-path: if all handlers complete synchronously, avoid async state machine.
            for (int i = 0; i < handlers.Length; i++)
            {
                var task = handlers[i].Handle(notification, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    return DispatchRemainingAsync(handlers, notification, i, task, cancellationToken);
            }

            return Task.CompletedTask;
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
