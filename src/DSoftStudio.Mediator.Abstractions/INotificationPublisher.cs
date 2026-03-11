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
        Task Publish<TNotification>(
            IEnumerable<INotificationHandler<TNotification>> handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification;
    }
}
