// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Default notification publisher. Invokes handlers one at a time in registration order.
    /// If a handler throws, subsequent handlers are not invoked.
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
