// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.DomainEvents.Application.Events.Handlers;

/// <summary>
/// Sends a welcome email when a user registers.
/// </summary>
public sealed class SendWelcomeEmailHandler(
    ILogger<SendWelcomeEmailHandler> logger)
    : INotificationHandler<UserRegisteredEvent>
{
    public Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[Email] Sending welcome email to {Email} (UserId: {UserId})",
            notification.Email,
            notification.UserId);

        return Task.CompletedTask;
    }
}
