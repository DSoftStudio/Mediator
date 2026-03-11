// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.DomainEvents.Application.Events.Handlers;

/// <summary>
/// Creates an audit log entry when a user registers.
/// </summary>
public sealed class AuditLogHandler(
    ILogger<AuditLogHandler> logger)
    : INotificationHandler<UserRegisteredEvent>
{
    public Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[Audit] User {UserId} registered with {Email} at {Time}",
            notification.UserId,
            notification.Email,
            DateTime.UtcNow);

        return Task.CompletedTask;
    }
}
