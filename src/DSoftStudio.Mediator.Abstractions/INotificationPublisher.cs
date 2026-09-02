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
    /// The default implementation (<c>SequentialNotificationPublisher</c>) invokes handlers
    /// one at a time in registration order.
    /// </para>
    /// </summary>
    public interface INotificationPublisher
    {
        /// <summary>
        /// Invokes <paramref name="handlers"/> for <paramref name="notification"/>, in whatever
        /// order and concurrency this strategy defines.
        /// </summary>
        /// <remarks>
        /// The built-in <c>SequentialNotificationPublisher</c> invokes them one at a time in
        /// registration order, stopping if one throws. <c>ParallelNotificationPublisher</c> starts
        /// them all instead.
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
