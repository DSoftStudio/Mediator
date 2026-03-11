// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Publishes notifications to all registered handlers.
    /// <para>
    /// Use this interface when a component only needs to publish notifications
    /// without sending requests. Follows the Interface Segregation Principle.
    /// </para>
    /// </summary>
    public interface IPublisher
    {
        /// <summary>
        /// Publishes a notification to all registered <see cref="INotificationHandler{TNotification}"/> instances.
        /// </summary>
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification;

        /// <summary>
        /// Publishes a notification whose compile-time type is unknown (runtime dispatch).
        /// The object must implement <see cref="INotification"/>.
        /// </summary>
        Task Publish(object notification, CancellationToken cancellationToken = default);
    }
}
