// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

public class NotificationPublisherFlagTests
{
    /// <summary>
    /// When a custom <see cref="INotificationPublisher"/> is registered,
    /// resolving <see cref="IMediator"/> must set the global
    /// <see cref="NotificationPublisherFlag.HasCustomPublisher"/> flag so
    /// generated interceptors can skip the per-call DI probe.
    /// </summary>
    [Fact]
    public void Flag_IsSet_WhenCustomPublisherIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();

        using var provider = services.BuildServiceProvider();

        // Resolving IMediator triggers the Mediator constructor which sets the flag.
        _ = provider.GetRequiredService<IMediator>();

        NotificationPublisherFlag.HasCustomPublisher.ShouldBeTrue();
    }

    /// <summary>
    /// <see cref="NotificationPublisherFlag.DetectFrom"/> probes DI without
    /// requiring the <see cref="Mediator"/> constructor. This covers the
    /// code path used by generated PrecompileNotifications().
    /// </summary>
    [Fact]
    public void DetectFrom_SetsFlag_WhenPublisherIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();

        using var provider = services.BuildServiceProvider();

        NotificationPublisherFlag.DetectFrom(provider);

        NotificationPublisherFlag.HasCustomPublisher.ShouldBeTrue();
    }
}
