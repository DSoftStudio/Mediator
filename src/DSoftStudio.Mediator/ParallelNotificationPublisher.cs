// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Invokes all notification handlers in parallel using <see cref="Task.WhenAll"/>.
    /// All handlers start concurrently; awaiting the returned task surfaces the first faulting
    /// handler's exception (the remaining failures are available on the task's
    /// <see cref="Task.Exception"/> aggregate) once every handler has completed.
    /// </summary>
    public sealed class ParallelNotificationPublisher : INotificationPublisher
    {
        public Task Publish<TNotification>(
            IEnumerable<INotificationHandler<TNotification>> handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            // Materialize once to get count — MS DI returns an array, so this is free.
            var array = handlers is INotificationHandler<TNotification>[] a
                ? a
                : System.Linq.Enumerable.ToArray(handlers);

            if (array.Length == 0)
                return Task.CompletedTask;

            var tasks = new Task[array.Length];
            for (int i = 0; i < array.Length; i++)
                tasks[i] = array[i].Handle(notification, cancellationToken);

            return Task.WhenAll(tasks);
        }
    }
}
