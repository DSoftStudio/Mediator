// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.DomainEvents.Application.Events.Handlers;

/// <summary>
/// Provisions default settings for newly registered users.
/// </summary>
public sealed class ProvisionUserDefaultsHandler(
    ILogger<ProvisionUserDefaultsHandler> logger)
    : INotificationHandler<UserRegisteredEvent>
{
    public Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[Provisioning] Creating default settings for user {UserId}",
            notification.UserId);

        return Task.CompletedTask;
    }
}
