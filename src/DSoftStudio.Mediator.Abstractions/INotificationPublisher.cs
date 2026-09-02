// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Strategy for publishing notifications to handlers.
    /// <para>
    /// Register a custom implementation to control how notification handlers are invoked:
    /// sequential, parallel, fire-and-forget, or any custom strategy.
    /// </para>
    /// <para>
    /// By default NO publisher is registered: <c>Publish</c> goes through generated code that
    /// invokes the handlers one at a time and stops if one throws. Registering an implementation of
    /// this interface takes that route instead, which costs a container lookup per publish and
    /// resolves the handlers through <c>IEnumerable&lt;INotificationHandler&lt;T&gt;&gt;</c> rather
    /// than through the generated concrete factories.
    /// </para>
    /// <para>
    /// The built-in <c>SequentialNotificationPublisher</c> reproduces the default semantics, so
    /// registering it changes performance rather than behavior.
    /// </para>
    /// <para>
    /// Registering a publisher also changes WHICH handlers run, not only how. The default path
    /// dispatches the table the generator built at compile time; a publisher is handed whatever the
    /// container returns. A handler the generator could not see — one registered by hand against
    /// <see cref="INotificationHandler{TNotification}"/> — is therefore skipped by default and
    /// invoked once a publisher is registered.
    /// </para>
    /// </summary>
    public interface INotificationPublisher
    {
        /// <summary>
        /// Invokes <paramref name="handlers"/> for <paramref name="notification"/>, in whatever
        /// order and concurrency this strategy defines.
        /// </summary>
        /// <remarks>
        /// <paramref name="handlers"/> arrives in the order the container returns it, which is
        /// registration order for the default container. The built-in
        /// <c>SequentialNotificationPublisher</c> invokes them one at a time in that order, stopping
        /// if one throws. <c>ParallelNotificationPublisher</c> queues each of them to the thread pool
        /// and awaits them together, so they run concurrently whether or not they suspend.
        /// </remarks>
        /// <typeparam name="TNotification">The notification type.</typeparam>
        /// <param name="handlers">The handlers registered for the notification type.</param>
        /// <param name="notification">The notification to deliver.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when the strategy considers delivery finished.</returns>
        Task Publish<TNotification>(
            IEnumerable<INotificationHandler<TNotification>> handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification;
    }
}
