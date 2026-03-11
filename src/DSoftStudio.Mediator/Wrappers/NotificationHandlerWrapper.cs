// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Wrappers
{

    /// <summary>
    /// Non-generic base class for notification dispatching via runtime type dispatch.
    /// </summary>
    internal abstract class NotificationHandlerWrapper
    {
        public abstract Task Handle(object notification, IServiceProvider serviceProvider, INotificationPublisher? publisher, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Strongly-typed wrapper that dispatches notifications.
    /// Routes through <see cref="INotificationPublisher"/> when registered,
    /// otherwise uses compile-time generated dispatch tables.
    /// </summary>
    internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
        where TNotification : INotification
    {
        public override Task Handle(
          object notification,
          IServiceProvider serviceProvider,
          INotificationPublisher? publisher,
          CancellationToken cancellationToken)
        {
            var typed = (TNotification)notification;

            if (publisher is not null)
            {
                var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
                return publisher.Publish(handlers, typed, cancellationToken);
            }

            return NotificationDispatcher.DispatchSequential(
                typed,
                serviceProvider,
                cancellationToken);
        }
    }
}

