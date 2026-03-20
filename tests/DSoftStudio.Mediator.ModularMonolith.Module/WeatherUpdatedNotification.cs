// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.ModularMonolith.Module;

/// <summary>
/// Public notification contract — visible to the host.
/// </summary>
public sealed record WeatherUpdatedNotification(string City) : INotification;

/// <summary>
/// INTERNAL notification handler — should be skipped by the host's generator
/// (same CS0122 prevention as query/command handlers).
/// </summary>
internal sealed class WeatherUpdatedNotificationHandler : INotificationHandler<WeatherUpdatedNotification>
{
    public Task Handle(WeatherUpdatedNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
