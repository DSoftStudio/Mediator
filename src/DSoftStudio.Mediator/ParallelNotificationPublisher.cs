// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Runs every notification handler concurrently, awaiting them together with
    /// <see cref="Task.WhenAll(IEnumerable{Task})"/>.
    /// <para>
    /// Each handler is queued to the thread pool, so handlers run in parallel whether or not they
    /// suspend — including ones written in the synchronous <c>return Task.CompletedTask</c> style
    /// that <see cref="INotificationHandler{TNotification}"/> recommends. Handlers must therefore be
    /// safe to run alongside each other.
    /// </para>
    /// <para>
    /// Every handler is started even when an earlier one fails. Awaiting the returned task surfaces
    /// the first faulting handler's exception; the remaining failures are available on the task's
    /// <see cref="Task.Exception"/> aggregate.
    /// </para>
    /// <para>
    /// This is opt-in and costs a thread-pool work item per handler. The default dispatch path runs
    /// handlers one at a time on the calling thread with no such cost.
    /// </para>
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
            {
                var handler = array[i];

                // Queued rather than invoked inline. Invoking inline would only overlap handlers
                // that actually suspend: a handler doing synchronous work would run to completion
                // before the next one started, which is not what this type is named for.
                //
                // Task.Run also converts a synchronous throw into a faulted task, so one failing
                // handler can neither stop the others from starting nor leave an already-started
                // handler unobserved (an unawaited faulted task surfaces as UnobservedTaskException).
                //
                // CancellationToken.None is deliberate, and is exactly what Task.Run(Func<Task>)
                // uses on its own -- this changes no behaviour. Handing Task.Run the REAL token would
                // let the scheduler cancel a queued work item before the handler ever ran, dropping
                // that handler silently; the contract here is that every registered handler is
                // invoked and observes cancellation itself, inside Handle.
                //
                // Written into the call rather than left to the paragraph above it, because that is
                // the only form a reader skimming one line -- or an analyzer -- can see.
                tasks[i] = Task.Run(
                    () => handler.Handle(notification, cancellationToken),
                    CancellationToken.None);
            }

            return Task.WhenAll(tasks);
        }
    }
}
