// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Marker interface for a notification (domain event, in-process message).
    /// Multiple handlers can subscribe to the same notification type.
    /// </summary>
    public interface INotification { };

}

