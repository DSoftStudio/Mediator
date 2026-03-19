// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.InternalsVisibleTo.Host;

/// <summary>
/// Notification handler — tests that notification dispatch tables also don't conflict.
/// </summary>
public record PingNotification : INotification;

public sealed class PingNotificationHandler : INotificationHandler<PingNotification>
{
    public Task Handle(PingNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
