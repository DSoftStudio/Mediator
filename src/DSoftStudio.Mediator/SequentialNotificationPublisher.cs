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
            foreach (var handler in handlers)
            {
                var task = handler.Handle(notification, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    return AwaitRemaining(task, handlers, handler, notification, cancellationToken);
            }

            return Task.CompletedTask;
        }

        private static async Task AwaitRemaining<TNotification>(
            Task pendingTask,
            IEnumerable<INotificationHandler<TNotification>> handlers,
            INotificationHandler<TNotification> current,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            await pendingTask.ConfigureAwait(false);

            bool found = false;
            foreach (var handler in handlers)
            {
                if (!found)
                {
                    if (ReferenceEquals(handler, current))
                        found = true;
                    continue;
                }

                await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
