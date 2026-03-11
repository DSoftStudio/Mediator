// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Dedicated notification to avoid static state collisions ───────

public record FloodNotification : INotification;

public sealed class FloodNotificationHandler : INotificationHandler<FloodNotification>
{
    private int _callCount;
    public int CallCount => _callCount;

    public Task Handle(FloodNotification notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Task.CompletedTask;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

public class NotificationFloodTests
{
    [Fact]
    public async Task Publish_10000ConcurrentNotifications_NoExceptions()
    {
        var handler = new FloodNotificationHandler();

        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompileNotifications();
        services.AddSingleton<INotificationHandler<FloodNotification>>(handler);
        services.AddSingleton(handler);

        using var provider = services.BuildServiceProvider();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10_000),
            async (_, _) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Publish(new FloodNotification());
            });

        handler.CallCount.ShouldBe(10_000);
    }

    [Fact]
    public async Task Publish_10000ConcurrentNotifications_MultipleHandlers_AllInvoked()
    {
        var handler1 = new FloodNotificationHandler();
        var handler2 = new FloodNotificationHandler();

        var services = new ServiceCollection();
        services.AddMediator();
        services.AddSingleton<INotificationPublisher, SequentialNotificationPublisher>();
        services.AddSingleton<INotificationHandler<FloodNotification>>(handler1);
        services.AddSingleton<INotificationHandler<FloodNotification>>(handler2);

        using var provider = services.BuildServiceProvider();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10_000),
            async (_, _) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Publish(new FloodNotification());
            });

        handler1.CallCount.ShouldBe(10_000);
        handler2.CallCount.ShouldBe(10_000);
    }
}
