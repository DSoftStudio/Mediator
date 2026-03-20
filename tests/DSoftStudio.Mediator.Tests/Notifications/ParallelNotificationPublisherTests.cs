// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

// ── Unique notification types per test scenario ────────────────────

public sealed record ParallelPing : INotification;
public sealed record ParallelSlowPing : INotification;
public sealed record ParallelThrowPing : INotification;

// ── Handlers (non-throwing only — these get auto-registered) ───────

public sealed class ParallelHandlerA : INotificationHandler<ParallelPing>
{
    public int CallCount;
    public Task Handle(ParallelPing notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

public sealed class ParallelHandlerB : INotificationHandler<ParallelPing>
{
    public int CallCount;
    public Task Handle(ParallelPing notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

public sealed class SlowParallelHandlerA : INotificationHandler<ParallelSlowPing>
{
    public int CallCount;
    public async Task Handle(ParallelSlowPing notification, CancellationToken ct)
    {
        ConcurrencyTracker.Enter();
        await Task.Delay(50, ct);
        ConcurrencyTracker.Exit();
        Interlocked.Increment(ref CallCount);
    }
}

public sealed class SlowParallelHandlerB : INotificationHandler<ParallelSlowPing>
{
    public int CallCount;
    public async Task Handle(ParallelSlowPing notification, CancellationToken ct)
    {
        ConcurrencyTracker.Enter();
        await Task.Delay(50, ct);
        ConcurrencyTracker.Exit();
        Interlocked.Increment(ref CallCount);
    }
}

public sealed class ThrowParallelHandler : INotificationHandler<ParallelThrowPing>
{
    public Task Handle(ParallelThrowPing notification, CancellationToken ct)
        => Task.FromException(new InvalidOperationException("boom"));
}

public sealed class SafeParallelHandler : INotificationHandler<ParallelThrowPing>
{
    public int CallCount;
    public Task Handle(ParallelThrowPing notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

// ── Concurrency tracker (timing-free parallel proof) ───────────────

internal static class ConcurrencyTracker
{
    private static int _active;
    private static int _peak;

    public static int Peak => Volatile.Read(ref _peak);

    public static void Reset()
    {
        Volatile.Write(ref _active, 0);
        Volatile.Write(ref _peak, 0);
    }

    public static void Enter()
    {
        var current = Interlocked.Increment(ref _active);
        // Update peak via CAS loop
        int oldPeak;
        do { oldPeak = Volatile.Read(ref _peak); }
        while (current > oldPeak && Interlocked.CompareExchange(ref _peak, current, oldPeak) != oldPeak);
    }

    public static void Exit() => Interlocked.Decrement(ref _active);
}

// ── Tests ───────────────────────────────────────────────────────────

public class ParallelNotificationPublisherTests
{
    private static ServiceProvider BuildProvider<TNotification>(params INotificationHandler<TNotification>[] handlers)
        where TNotification : INotification
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();

        foreach (var h in handlers)
            services.AddSingleton(h);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Publish_InvokesAllHandlers()
    {
        var a = new ParallelHandlerA();
        var b = new ParallelHandlerB();
        using var provider = BuildProvider(a, b);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new ParallelPing());

        a.CallCount.ShouldBe(1);
        b.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_HandlersRunInParallel()
    {
        var a = new SlowParallelHandlerA();
        var b = new SlowParallelHandlerB();
        using var provider = BuildProvider(a, b);
        var mediator = provider.GetRequiredService<IMediator>();

        ConcurrencyTracker.Reset();
        await mediator.Publish(new ParallelSlowPing());

        // Both handlers call Enter() synchronously before the first await,
        // so peak >= 2 proves they overlapped — no wall-clock timing needed.
        ConcurrencyTracker.Peak.ShouldBeGreaterThanOrEqualTo(2);
        a.CallCount.ShouldBe(1);
        b.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_HandlerThrows_PropagatesException()
    {
        using var provider = BuildProvider<ParallelThrowPing>(new ThrowParallelHandler(), new SafeParallelHandler());
        var mediator = provider.GetRequiredService<IMediator>();

        Func<Task> act = () => mediator.Publish(new ParallelThrowPing());

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task PublishObject_WithParallelPublisher_InvokesAllHandlers()
    {
        var a = new ParallelHandlerA();
        var b = new ParallelHandlerB();
        using var provider = BuildProvider(a, b);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish((object)new ParallelPing());

        a.CallCount.ShouldBe(1);
        b.CallCount.ShouldBe(1);
    }
}
