// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles a notification of type <typeparamref name="TNotification"/>.
    /// Multiple handlers can be registered for the same notification type;
    /// all will be invoked sequentially when the notification is published.
    /// </summary>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken);
    }
}


