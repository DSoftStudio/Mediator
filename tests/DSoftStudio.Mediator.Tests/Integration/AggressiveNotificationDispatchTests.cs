// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════
//  TEST-LOCAL TYPES
// ═══════════════════════════════════════════════════════════════════

/// <summary>Shared, cleared per test — the collection below is serialized.</summary>
public static class AggNoteLog
{
    private static readonly ConcurrentQueue<string> Entries = new();
    public static void Record(string entry) => Entries.Enqueue(entry);
    public static string[] Snapshot() => [.. Entries];
    public static void Clear() => Entries.Clear();
}

public sealed record AggNote(int N) : INotification;

/// <summary>Stateless → auto-Singleton → part of an AGGRESSIVE-eligible fan-out.</summary>
public sealed class AggNoteHandlerA : INotificationHandler<AggNote>
{
    public Task Handle(AggNote notification, CancellationToken ct)
    {
        AggNoteLog.Record($"A:{notification.N}");
        return Task.CompletedTask;
    }
}

public sealed class AggNoteHandlerB : INotificationHandler<AggNote>
{
    public Task Handle(AggNote notification, CancellationToken ct)
    {
        AggNoteLog.Record($"B:{notification.N}");
        return Task.CompletedTask;
    }
}

public sealed record AggYieldNote() : INotification;

/// <summary>
/// Truly-async first handler: the armed unrolled dispatch must PRESERVE the sequential
/// await-then-continue contract — B starts only after A fully completes (AwaitTail semantics).
/// </summary>
public sealed class AggYieldHandlerA : INotificationHandler<AggYieldNote>
{
    public async Task Handle(AggYieldNote notification, CancellationToken ct)
    {
        AggNoteLog.Record("A:start");
        await Task.Yield();
        await Task.Delay(10, ct);
        AggNoteLog.Record("A:end");
    }
}

public sealed class AggYieldHandlerB : INotificationHandler<AggYieldNote>
{
    public Task Handle(AggYieldNote notification, CancellationToken ct)
    {
        AggNoteLog.Record("B:run");
        return Task.CompletedTask;
    }
}

public sealed record AggScopedNote() : INotification;

/// <summary>Scoped mutable state → never AGGRESSIVE-eligible; scopes must get fresh instances.</summary>
[HandlerLifetime(HandlerLifetime.Scoped)]
public sealed class AggScopedNoteHandler : INotificationHandler<AggScopedNote>
{
    private int _calls;
    public Task Handle(AggScopedNote notification, CancellationToken ct)
    {
        AggNoteLog.Record($"scoped:{++_calls}");
        return Task.CompletedTask;
    }
}

public sealed record AggReverifyNote() : INotification;

/// <summary>
/// Singleton by attribute, stateful — proves the arm-time re-verify: re-registered as Scoped
/// AFTER PrecompileNotifications, arming must fail and scopes keep getting fresh instances.
/// </summary>
[HandlerLifetime(HandlerLifetime.Singleton)]
public sealed class AggReverifyNoteHandler : INotificationHandler<AggReverifyNote>
{
    private int _calls;
    public Task Handle(AggReverifyNote notification, CancellationToken ct)
    {
        AggNoteLog.Record($"reverify:{++_calls}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// ADR-0066 AGGRESSIVE Publish tier: eligibility from the generated
/// PrecompileNotifications scan, lazy one-shot arming with arm-time re-verification, the
/// second-container poison latch, and the late-custom-publisher disarm hook.
/// Joins the serialized AggressiveDispatch collection — the latch is process-global.
/// </summary>
[Collection("AggressiveDispatch")]
public class AggressiveNotificationDispatchTests
{
    private static void ResetTierState()
    {
        AggressiveDispatchLatch.ResetForTests();
        NotificationPublisherFlag.ResetForTests();
        AggressiveNotificationDispatch<AggNote>.ResetForTests();
        AggressiveNotificationDispatch<AggYieldNote>.ResetForTests();
        AggressiveNotificationDispatch<AggScopedNote>.ResetForTests();
        AggressiveNotificationDispatch<AggReverifyNote>.ResetForTests();
        AggNoteLog.Clear();
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection>? beforePrecompile = null,
        Action<IServiceCollection>? postRegistration = null)
    {
        var services = new ServiceCollection();
        services
            .AddMediator()
            .RegisterMediatorHandlers();
        beforePrecompile?.Invoke(services);
        services.PrecompileNotifications();
        postRegistration?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Singleton_FanOut_Arms_On_First_Publish_And_Stays_Correct()
    {
        ResetTierState();
        await using var sp = BuildProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        AggressiveNotificationDispatch<AggNote>.ShouldAttemptArm.ShouldBeTrue(
            "a stateless (auto-Singleton) fan-out with no custom publisher must be eligible");

        int armedBefore = AggressiveDispatchLatch.ArmedCount;

        // The object path routes through the generated PublishObjectSwitch — the tier's entry
        // point when interceptors are suppressed (this test project).
        await mediator.Publish((object)new AggNote(1), TestContext.Current.CancellationToken);

        AggressiveDispatchLatch.ArmedCount.ShouldBe(armedBefore + 1,
            "the first publish must arm the notification holder");
        AggressiveNotificationDispatch<AggNote>.ShouldAttemptArm.ShouldBeFalse(
            "arming is one-shot");

        await mediator.Publish((object)new AggNote(2), TestContext.Current.CancellationToken);

        AggNoteLog.Snapshot().ShouldBe(["A:1", "B:1", "A:2", "B:2"],
            "generated dispatch order (alphabetical) must hold on both the arming and armed paths");
    }

    [Fact]
    public async Task Armed_FanOut_Preserves_Sequential_Await_Then_Continue()
    {
        ResetTierState();
        await using var sp = BuildProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        // Arm (first publish) and verify ordering on BOTH publishes: B must never start
        // before the yielding A completes.
        await mediator.Publish((object)new AggYieldNote(), TestContext.Current.CancellationToken);
        await mediator.Publish((object)new AggYieldNote(), TestContext.Current.CancellationToken);

        AggNoteLog.Snapshot().ShouldBe(["A:start", "A:end", "B:run", "A:start", "A:end", "B:run"],
            "the armed unrolled dispatch must keep DispatchSequential's await-then-continue contract");
    }

    [Fact]
    public async Task Second_Container_Poisons_And_Disarms_Notification_Holder()
    {
        ResetTierState();

        await using var spA = BuildProvider();
        var mediatorA = spA.GetRequiredService<IMediator>();
        await mediatorA.Publish((object)new AggNote(1), TestContext.Current.CancellationToken); // arms

        int armedAfterArm = AggressiveDispatchLatch.ArmedCount;

        await using var spB = BuildProvider(); // second container -> poison during registration
        AggressiveDispatchLatch.IsPoisoned.ShouldBeTrue();
        AggressiveDispatchLatch.ArmedCount.ShouldBe(armedAfterArm - 1,
            "the poison must disarm the notification holder (gauge is current state)");

        // Both containers keep publishing correctly on the SAFE tier.
        var mediatorB = spB.GetRequiredService<IMediator>();
        await mediatorA.Publish((object)new AggNote(2), TestContext.Current.CancellationToken);
        await mediatorB.Publish((object)new AggNote(3), TestContext.Current.CancellationToken);

        AggNoteLog.Snapshot().ShouldBe(["A:1", "B:1", "A:2", "B:2", "A:3", "B:3"]);
    }

    [Fact]
    public async Task Late_Custom_Publisher_Disarms_Armed_Holder()
    {
        ResetTierState();
        await using var sp = BuildProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish((object)new AggNote(1), TestContext.Current.CancellationToken); // arms
        int armedAfterArm = AggressiveDispatchLatch.ArmedCount;

        // The supported late-registration path signals the flag — which must disarm FIRST.
        NotificationPublisherFlag.MarkRegistered();

        AggressiveDispatchLatch.ArmedCount.ShouldBe(armedAfterArm - 1,
            "a custom publisher changes Publish semantics — armed notification holders must stand down");
        NotificationPublisherFlag.HasCustomPublisher.ShouldBeTrue();

        // No publisher is actually resolvable in this container -> SAFE tier, still correct.
        await mediator.Publish((object)new AggNote(2), TestContext.Current.CancellationToken);
        AggNoteLog.Snapshot().ShouldBe(["A:1", "B:1", "A:2", "B:2"]);
    }

    [Fact]
    public void Publisher_Registered_Before_Precompile_Blocks_Eligibility()
    {
        ResetTierState();
        using var sp = BuildProvider(beforePrecompile: static services =>
            services.AddSingleton<INotificationPublisher, RecordingPublisher>());

        AggressiveNotificationDispatch<AggNote>.ShouldAttemptArm.ShouldBeFalse(
            "an INotificationPublisher descriptor present at Precompile time must block eligibility");
    }

    [Fact]
    public async Task Scoped_Handler_Never_Eligible_And_Scopes_Stay_Fresh()
    {
        ResetTierState();
        await using var sp = BuildProvider();

        AggressiveNotificationDispatch<AggScopedNote>.ShouldAttemptArm.ShouldBeFalse(
            "a Scoped handler must never be AGGRESSIVE-eligible");

        using (var scope1 = sp.CreateScope())
            await scope1.ServiceProvider.GetRequiredService<IMediator>()
                .Publish((object)new AggScopedNote(), TestContext.Current.CancellationToken);
        using (var scope2 = sp.CreateScope())
            await scope2.ServiceProvider.GetRequiredService<IMediator>()
                .Publish((object)new AggScopedNote(), TestContext.Current.CancellationToken);

        AggNoteLog.Snapshot().ShouldBe(["scoped:1", "scoped:1"],
            "each scope must get a FRESH handler instance (counter restarts)");
    }

    [Fact]
    public async Task PostPrecompile_Lifetime_Narrowing_Fails_ArmTime_Reverify()
    {
        ResetTierState();
        await using var sp = BuildProvider(postRegistration: static services =>
            services.AddScoped<AggReverifyNoteHandler>());

        // Eligibility was computed at Precompile (Singleton was winning); the LAST descriptor
        // is now Scoped — the arm-time re-verify must refuse.
        AggressiveNotificationDispatch<AggReverifyNote>.ShouldAttemptArm.ShouldBeTrue();

        int armedBefore = AggressiveDispatchLatch.ArmedCount;

        using (var scope1 = sp.CreateScope())
            await scope1.ServiceProvider.GetRequiredService<IMediator>()
                .Publish((object)new AggReverifyNote(), TestContext.Current.CancellationToken);
        using (var scope2 = sp.CreateScope())
            await scope2.ServiceProvider.GetRequiredService<IMediator>()
                .Publish((object)new AggReverifyNote(), TestContext.Current.CancellationToken);

        AggressiveDispatchLatch.ArmedCount.ShouldBe(armedBefore,
            "arming must fail the descriptor re-verification (Scoped won last)");
        AggNoteLog.Snapshot().ShouldBe(["reverify:1", "reverify:1"],
            "scopes must keep getting fresh instances — nothing may pin the first scope's handler");
    }

    [Fact]
    public async Task Typed_Publish_Path_Stays_Correct_Alongside_Armed_Object_Path()
    {
        ResetTierState();
        await using var sp = BuildProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish((object)new AggNote(1), TestContext.Current.CancellationToken); // arms
        await mediator.Publish(new AggNote(2), TestContext.Current.CancellationToken);         // typed (virtual path)
        await mediator.Publish((object)new AggNote(3), TestContext.Current.CancellationToken); // armed

        AggNoteLog.Snapshot().ShouldBe(["A:1", "B:1", "A:2", "B:2", "A:3", "B:3"]);
    }

    private sealed class RecordingPublisher : INotificationPublisher
    {
        public async Task Publish<TNotification>(
            IEnumerable<INotificationHandler<TNotification>> handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            foreach (var handler in handlers)
                await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
