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

        await mediator.Publish(new ParallelPing(), TestContext.Current.CancellationToken);

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
        await mediator.Publish(new ParallelSlowPing(), TestContext.Current.CancellationToken);

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

        Func<Task> act = () => mediator.Publish(new ParallelThrowPing(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task Publish_HandlerThrowsSynchronously_StillStartsTheRemainingHandlers()
    {
        var safe = new SafeParallelHandler();
        var publisher = new ParallelNotificationPublisher();

        // The throwing handler goes FIRST. Its throw is synchronous, not a faulted task: before the
        // fix it escaped the start loop, so `safe` never ran and anything already started was left
        // unobserved. Calling Publish must not throw here — the failure belongs on the task.
        var task = publisher.Publish<ParallelThrowPing>(
            new INotificationHandler<ParallelThrowPing>[] { new SyncThrowParallelHandler(), safe },
            new ParallelThrowPing(),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(async () => await task);
        safe.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_SynchronousHandlers_ActuallyRunConcurrently()
    {
        using var gate = new CountdownEvent(2);
        var a = new SyncOverlapHandler(gate);
        var b = new SyncOverlapHandler(gate);
        var publisher = new ParallelNotificationPublisher();

        // Neither handler awaits anything. If the publisher invoked them inline on the calling
        // thread, the first would block on a gate the second cannot reach until it returns, and the
        // wait would time out. Passing proves the handlers really are on separate threads.
        await publisher.Publish<SyncOverlapPing>(
            new INotificationHandler<SyncOverlapPing>[] { a, b },
            new SyncOverlapPing(),
            TestContext.Current.CancellationToken);

        a.SawTheOther.ShouldBeTrue();
        b.SawTheOther.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishObject_WithParallelPublisher_InvokesAllHandlers()
    {
        var a = new ParallelHandlerA();
        var b = new ParallelHandlerB();
        using var provider = BuildProvider(a, b);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish((object)new ParallelPing(), TestContext.Current.CancellationToken);

        a.CallCount.ShouldBe(1);
        b.CallCount.ShouldBe(1);
    }
}

// `file`-scoped so handler discovery skips it: this handler is fed to the publisher directly and
// must not join the auto-registered set for ParallelThrowPing.
file sealed class SyncThrowParallelHandler : INotificationHandler<ParallelThrowPing>
{
    public Task Handle(ParallelThrowPing notification, CancellationToken ct)
        => throw new InvalidOperationException("sync boom");
}

// `file`-scoped so handler discovery skips them: these are fed to the publisher directly and must
// not join the auto-registered set.
file sealed record SyncOverlapPing : INotification;

file sealed class SyncOverlapHandler(CountdownEvent gate) : INotificationHandler<SyncOverlapPing>
{
    public bool SawTheOther;

    public Task Handle(SyncOverlapPing notification, CancellationToken ct)
    {
        // Fully synchronous — no await anywhere. The only way this handler can observe the other
        // one arriving is if the publisher runs them on different threads.
        gate.Signal();
        SawTheOther = gate.Wait(TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }
}
