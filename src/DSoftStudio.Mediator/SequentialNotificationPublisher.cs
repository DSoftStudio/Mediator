// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Invokes the handlers one at a time, each completing before the next starts. If a handler
    /// throws, the ones after it are not invoked.
    /// <para>
    /// Despite the name this is NOT what runs by default: by default no
    /// <see cref="INotificationPublisher"/> is registered at all and <c>Publish</c> goes through
    /// generated dispatch, which implements the same semantics. This type exists for callers that
    /// need an <see cref="INotificationPublisher"/> INSTANCE — the OpenTelemetry bridge wraps one —
    /// and registering it in DI is a de-optimization: it disarms the generated fast path and resolves
    /// the handlers from the container on every publish, to arrive at identical behavior.
    /// </para>
    /// <para>
    /// The order is whatever the container returns, which for generator-registered handlers is
    /// handler type name order — not the order the registrations appear in.
    /// </para>
    /// </summary>
    public sealed class SequentialNotificationPublisher : INotificationPublisher
    {
        public Task Publish<TNotification>(
            IEnumerable<INotificationHandler<TNotification>> handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            // Materialize once — MS DI returns an array, so this cast is free.
            var array = handlers is INotificationHandler<TNotification>[] a
                ? a
                : System.Linq.Enumerable.ToArray(handlers);

            for (int i = 0; i < array.Length; i++)
            {
                var task = array[i].Handle(notification, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    return AwaitRemaining(task, array, notification, i, cancellationToken);
            }

            return Task.CompletedTask;
        }

        private static async Task AwaitRemaining<TNotification>(
            Task pendingTask,
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            int currentIndex,
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
