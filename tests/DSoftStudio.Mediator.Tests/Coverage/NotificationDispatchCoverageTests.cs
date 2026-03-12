// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Unique types ──
// These types are discovered by the source generator — their handlers are
// registered automatically by RegisterMediatorHandlers().

public record CovDispatchNotif : INotification;
public record CovDispatchAsyncNotif : INotification;
public record CovObjDispatchNotif : INotification;

// ── Sync handler (registered by generator as Singleton — no ctor params) ──

public sealed class CovDispatchNotifHandler : INotificationHandler<CovDispatchNotif>
{
    public static int CallCount;
    public Task Handle(CovDispatchNotif notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

// ── Async handlers ──

public sealed class CovDispatchAsyncNotifHandler1 : INotificationHandler<CovDispatchAsyncNotif>
{
    public static int CallCount;
    public async Task Handle(CovDispatchAsyncNotif notification, CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref CallCount);
    }
}

public sealed class CovDispatchAsyncNotifHandler2 : INotificationHandler<CovDispatchAsyncNotif>
{
    public static int CallCount;
    public async Task Handle(CovDispatchAsyncNotif notification, CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref CallCount);
    }
}

public sealed class CovObjDispatchNotifHandler : INotificationHandler<CovObjDispatchNotif>
{
    public static int CallCount;
    public Task Handle(CovObjDispatchNotif notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Covers NotificationDispatcher (internal), NotificationCachedDispatcher async paths,
/// and Publish(object) dispatch.
/// Handlers are source-generator discovered and auto-registered.
/// </summary>
public class NotificationDispatchCoverageTests
{
    [Fact]
    public async Task Publish_AsyncHandlers_ExercisesCachedDispatcherAsyncPath()
    {
        CovDispatchAsyncNotifHandler1.CallCount = 0;
        CovDispatchAsyncNotifHandler2.CallCount = 0;

        var services = new ServiceCollection();
        // No custom publisher → default path: NotificationCachedDispatcher
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish(new CovDispatchAsyncNotif());

        CovDispatchAsyncNotifHandler1.CallCount.ShouldBe(1);
        CovDispatchAsyncNotifHandler2.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_SyncHandler_DefaultPath_NoCustomPublisher()
    {
        CovDispatchNotifHandler.CallCount = 0;

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish(new CovDispatchNotif());

        CovDispatchNotifHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_Object_WithoutCustomPublisher_Dispatches()
    {
        CovObjDispatchNotifHandler.CallCount = 0;

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish((object)new CovObjDispatchNotif());

        CovObjDispatchNotifHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_Object_WithCustomPublisher_Dispatches()
    {
        CovObjDispatchNotifHandler.CallCount = 0;

        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish((object)new CovObjDispatchNotif());

        CovObjDispatchNotifHandler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_WithSequentialPublisher_AsyncHandlers()
    {
        CovDispatchAsyncNotifHandler1.CallCount = 0;
        CovDispatchAsyncNotifHandler2.CallCount = 0;

        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, SequentialNotificationPublisher>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish(new CovDispatchAsyncNotif());

        CovDispatchAsyncNotifHandler1.CallCount.ShouldBe(1);
        CovDispatchAsyncNotifHandler2.CallCount.ShouldBe(1);
    }
}
