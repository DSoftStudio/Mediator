// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════
//  TEST-LOCAL TYPES
// ═══════════════════════════════════════════════════════════════════

public sealed record AggPing(int N) : IRequest<string>;

/// <summary>Stateless → auto-Singleton → AGGRESSIVE-eligible.</summary>
public sealed class AggPingHandler : IRequestHandler<AggPing, string>
{
    public ValueTask<string> Handle(AggPing request, CancellationToken ct)
        => new("agg:" + request.N);
}

public sealed record AggScopedPing() : IRequest<int>;

/// <summary>Scoped mutable state → never AGGRESSIVE-eligible.</summary>
[HandlerLifetime(HandlerLifetime.Scoped)]
public sealed class AggScopedPingHandler : IRequestHandler<AggScopedPing, int>
{
    private int _calls;
    public ValueTask<int> Handle(AggScopedPing request, CancellationToken ct) => new(++_calls);
}

public sealed record AggReverifyPing() : IRequest<int>;

/// <summary>
/// Singleton by attribute, stateful — used to prove the arm-time re-verify: when the SAME
/// concrete type is re-registered as Scoped AFTER PrecompilePipelines, arming must fail and
/// scopes must keep getting fresh instances.
/// </summary>
[HandlerLifetime(HandlerLifetime.Singleton)]
public sealed class AggReverifyPingHandler : IRequestHandler<AggReverifyPing, int>
{
    private int _calls;
    public ValueTask<int> Handle(AggReverifyPing request, CancellationToken ct) => new(++_calls);
}

file sealed class AggReplacementHandler : IRequestHandler<AggPing, string>
{
    public ValueTask<string> Handle(AggPing request, CancellationToken ct)
        => new("replacement:" + request.N);
}

// The AGGRESSIVE latch is process-global one-way state; every container in the process feeds it.
// This collection runs with parallelization disabled so no other test's AddMediator can poison
// the latch between a ResetForTests() and the assertion that depends on it.
[CollectionDefinition("AggressiveDispatch", DisableParallelization = true)]
public sealed class AggressiveDispatchCollection;

/// <summary>
/// ADR-0065 AGGRESSIVE tier (default-ON, self-gating): eligibility from the generated
/// RegisterPipeline scan, lazy one-shot arming with arm-time re-verification, and the
/// second-container poison latch anchored in core AddMediator.
/// </summary>
[Collection("AggressiveDispatch")]
public class AggressiveDispatchTests
{
    private static void ResetTierState()
    {
        AggressiveDispatchLatch.ResetForTests();
        AggressiveDispatch<AggPing, string>.ResetForTests();
        AggressiveDispatch<AggScopedPing, int>.ResetForTests();
        AggressiveDispatch<AggReverifyPing, int>.ResetForTests();
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection>? postRegistration = null)
    {
        var services = new ServiceCollection();
        services
            .AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
        postRegistration?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Singleton_NoPipeline_Handler_Arms_On_First_Dispatch()
    {
        ResetTierState();
        await using var sp = BuildProvider();
        var sender = sp.GetRequiredService<ISender>();

        // Eligibility was granted by the PrecompilePipelines scan (Singleton + no chain).
        AggressiveDispatch<AggPing, string>.ShouldAttemptArm.ShouldBeTrue(
            "a stateless (auto-Singleton) no-pipeline handler must be AGGRESSIVE-eligible");

        (await sender.Send(new AggPing(1))).ShouldBe("agg:1");

        // The one-shot attempt consumed eligibility; the tier is not poisoned.
        AggressiveDispatch<AggPing, string>.ShouldAttemptArm.ShouldBeFalse(
            "arming is one-shot — the first dispatch consumes the attempt");
        AggressiveDispatchLatch.IsPoisoned.ShouldBeFalse();

        // Armed steady state stays correct.
        (await sender.Send(new AggPing(2))).ShouldBe("agg:2");
        (await sender.Send(new AggPing(3))).ShouldBe("agg:3");
    }

    [Fact]
    public async Task Second_Container_Poisons_And_Both_Containers_Stay_Correct()
    {
        ResetTierState();

        // Container A arms on first dispatch.
        await using var spA = BuildProvider();
        var senderA = spA.GetRequiredService<ISender>();
        (await senderA.Send(new AggPing(1))).ShouldBe("agg:1");

        // Container B (with a runtime override) poisons the tier DURING registration —
        // strictly before it can dispatch.
        await using var spB = BuildProvider(services =>
            services.AddTransient<IRequestHandler<AggPing, string>, AggReplacementHandler>());
        AggressiveDispatchLatch.IsPoisoned.ShouldBeTrue(
            "a second distinct IServiceCollection must poison the tier");

        var senderB = spB.GetRequiredService<ISender>();

        // Post-poison, every dispatch degrades to the SAFE tier: each container gets ITS handler.
        (await senderB.Send(new AggPing(2))).ShouldBe("replacement:2");
        (await senderA.Send(new AggPing(3))).ShouldBe("agg:3");
        (await senderB.Send(new AggPing(4))).ShouldBe("replacement:4");
    }

    [Fact]
    public async Task Scoped_Handler_Is_Never_Eligible_And_Scopes_Stay_Fresh()
    {
        ResetTierState();
        await using var sp = BuildProvider();

        AggressiveDispatch<AggScopedPing, int>.ShouldAttemptArm.ShouldBeFalse(
            "a Scoped handler must never be AGGRESSIVE-eligible");

        using (var scope1 = sp.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            (await sender.Send(new AggScopedPing())).ShouldBe(1);
            (await sender.Send(new AggScopedPing())).ShouldBe(2);
        }

        using (var scope2 = sp.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            (await sender.Send(new AggScopedPing())).ShouldBe(1,
                customMessage: "a new scope must get a fresh scoped handler — never a pinned one");
        }
    }

    [Fact]
    public async Task PostPrecompile_Lifetime_Narrowing_Fails_ArmTime_Reverify()
    {
        ResetTierState();

        // Same concrete type re-registered as SCOPED after PrecompilePipelines: eligibility was
        // granted from the (then-Singleton) scan, the resolved instance IS the expected concrete
        // type, so only the arm-time LAST-wins re-verification stands between us and pinning a
        // scoped instance process-wide.
        await using var sp = BuildProvider(services =>
            services.AddScoped<IRequestHandler<AggReverifyPing, int>, AggReverifyPingHandler>());

        AggressiveDispatch<AggReverifyPing, int>.ShouldAttemptArm.ShouldBeTrue(
            "precondition: the precompile-time scan saw a Singleton and granted eligibility");

        using (var scope1 = sp.CreateScope())
        {
            var sender = scope1.ServiceProvider.GetRequiredService<ISender>();
            (await sender.Send(new AggReverifyPing())).ShouldBe(1);
            (await sender.Send(new AggReverifyPing())).ShouldBe(2);
        }

        // The attempt ran and must have FAILED (last-wins descriptor is now Scoped).
        AggressiveDispatch<AggReverifyPing, int>.ShouldAttemptArm.ShouldBeFalse(
            "the arm attempt must be consumed");

        using (var scope2 = sp.CreateScope())
        {
            var sender = scope2.ServiceProvider.GetRequiredService<ISender>();
            (await sender.Send(new AggReverifyPing())).ShouldBe(1,
                customMessage: "arming must have failed — a fresh scope gets a fresh scoped instance");
        }
    }

    [Fact]
    public void Latch_Is_Idempotent_Per_Collection_And_OneWay_On_Second()
    {
        ResetTierState();

        var collectionA = new ServiceCollection();
        AggressiveDispatchLatch.OnContainerRegistered(collectionA);
        AggressiveDispatchLatch.OnContainerRegistered(collectionA); // repeat: same collection
        AggressiveDispatchLatch.IsPoisoned.ShouldBeFalse(
            "repeat AddMediator calls on the SAME collection must not poison");

        var collectionB = new ServiceCollection();
        AggressiveDispatchLatch.OnContainerRegistered(collectionB);
        AggressiveDispatchLatch.IsPoisoned.ShouldBeTrue(
            "a second DISTINCT collection must poison");

        bool armRan = false;
        AggressiveDispatchLatch.TryArm(() => armRan = true, () => { }).ShouldBeFalse(
            "TryArm after poison must refuse");
        armRan.ShouldBeFalse("the arm action must not run once poisoned");
    }

    [Fact]
    public async Task Arming_And_Poisoning_Update_Observability_Counters()
    {
        ResetTierState();
        int armedBefore = AggressiveDispatchLatch.ArmedCount;
        int poisonedBefore = AggressiveDispatchLatch.PoisonedCount;

        await using var spA = BuildProvider();
        var senderA = spA.GetRequiredService<ISender>();
        (await senderA.Send(new AggPing(1))).ShouldBe("agg:1"); // arms

        AggressiveDispatchLatch.ArmedCount.ShouldBe(armedBefore + 1,
            "arming must increment the aggressive-armed gauge");

        await using var spB = BuildProvider(); // second container -> poison

        AggressiveDispatchLatch.PoisonedCount.ShouldBe(poisonedBefore + 1,
            "poisoning must increment the aggressive-poisoned counter");
        AggressiveDispatchLatch.ArmedCount.ShouldBe(armedBefore,
            "the aggressive-armed gauge is CURRENT state — poison disarms every holder, " +
            "so the gauge must drop back, not stay frozen at its pre-poison value");
    }

    [Fact]
    public async Task StrictPoison_Switch_Throws_On_PoisonWhileArmed()
    {
        ResetTierState();
        AppContext.SetSwitch(AggressiveDispatchLatch.StrictPoisonSwitchName, true);
        try
        {
            await using var spA = BuildProvider();
            var senderA = spA.GetRequiredService<ISender>();
            (await senderA.Send(new AggPing(1))).ShouldBe("agg:1"); // arms

            // The second container's AddMediator must throw (fail-closed CI mode) — AFTER the
            // tier has already safely degraded.
            var ex = Should.Throw<InvalidOperationException>(() => BuildProvider());
            ex.Message.ShouldContain("second IServiceCollection");
            AggressiveDispatchLatch.IsPoisoned.ShouldBeTrue("the poison itself must still happen");
        }
        finally
        {
            AppContext.SetSwitch(AggressiveDispatchLatch.StrictPoisonSwitchName, false);
        }
    }

    [Fact]
    public void StrictPoison_Switch_Does_Not_Throw_When_Nothing_Was_Armed()
    {
        ResetTierState();
        AppContext.SetSwitch(AggressiveDispatchLatch.StrictPoisonSwitchName, true);
        try
        {
            var collectionA = new ServiceCollection();
            AggressiveDispatchLatch.OnContainerRegistered(collectionA);

            // No dispatch happened -> nothing armed -> multi-container is a legitimate pattern.
            var collectionB = new ServiceCollection();
            Should.NotThrow(() => AggressiveDispatchLatch.OnContainerRegistered(collectionB));
            AggressiveDispatchLatch.IsPoisoned.ShouldBeTrue();
        }
        finally
        {
            AppContext.SetSwitch(AggressiveDispatchLatch.StrictPoisonSwitchName, false);
        }
    }

    [Fact]
    public void Poison_Invokes_Registered_Disarm_Callbacks()
    {
        ResetTierState();

        var collectionA = new ServiceCollection();
        AggressiveDispatchLatch.OnContainerRegistered(collectionA);

        bool disarmed = false;
        AggressiveDispatchLatch.TryArm(() => { }, () => disarmed = true).ShouldBeTrue();
        disarmed.ShouldBeFalse();

        var collectionB = new ServiceCollection();
        AggressiveDispatchLatch.OnContainerRegistered(collectionB);

        disarmed.ShouldBeTrue("poisoning must fire every registered disarm callback");
    }
}

// ═══════════════════════════════════════════════════════════════════
//  BLK-1: losing eligibility must stand an ARMED holder down
// ═══════════════════════════════════════════════════════════════════

public sealed record BlkPing(int N) : IRequest<string>;

public sealed class BlkPingHandler : IRequestHandler<BlkPing, string>
{
    public ValueTask<string> Handle(BlkPing request, CancellationToken ct) => new("blk");
}

/// <summary>
/// The AGGRESSIVE holder returns the Singleton handler DIRECTLY, bypassing the pipeline chain.
/// That is only correct while the pair genuinely has no pipeline. If a later registration scan
/// reports the pair ineligible — a behavior, processor, exception handler or dispatch observer
/// appeared — an armed holder must stand down, or every one of those components silently never
/// runs and the dispatch looks perfectly healthy.
/// <para>
/// SetEligibility used to do <c>CompareExchange(_state, Ineligible, Eligible)</c>, which is a
/// no-op once the state is Attempted, and never touched the holder. The stand-down had to be
/// added before any re-planning step could reach this state.
/// </para>
/// </summary>
[Collection("AggressiveDispatch")]
public class AggressiveEligibilityStandDownTests
{
    [Fact]
    public void ArmedHolder_StandsDown_WhenPairStopsBeingEligible()
    {
        AggressiveDispatchLatch.ResetForTests();
        AggressiveDispatch<BlkPing, string>.ResetForTests();

        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<BlkPing, string>, BlkPingHandler>();

        AggressiveDispatchLatch.OnContainerRegistered(services);
        AggressiveDispatch<BlkPing, string>.SetEligibility(services, eligible: true);

        var armed = false;
        var disarmed = false;

        AggressiveDispatch<BlkPing, string>
            .TryArm(new BlkPingHandler(), () => armed = true, () => disarmed = true)
            .ShouldBeTrue("a Singleton handler with no pipeline is eligible and must arm");

        armed.ShouldBeTrue();
        disarmed.ShouldBeFalse();

        // A later scan finds a pipeline for this pair.
        AggressiveDispatch<BlkPing, string>.SetEligibility(services, eligible: false);

        disarmed.ShouldBeTrue(
            "an armed holder that stays armed after losing eligibility skips the whole chain");

        AggressiveDispatchLatch.ResetForTests();
        AggressiveDispatch<BlkPing, string>.ResetForTests();
    }

    [Fact]
    public void StandDown_IsTerminal_ALaterEligibleScanCannotReArm()
    {
        AggressiveDispatchLatch.ResetForTests();
        AggressiveDispatch<BlkPing, string>.ResetForTests();

        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<BlkPing, string>, BlkPingHandler>();

        AggressiveDispatchLatch.OnContainerRegistered(services);
        AggressiveDispatch<BlkPing, string>.SetEligibility(services, eligible: true);
        AggressiveDispatch<BlkPing, string>.TryArm(new BlkPingHandler(), () => { }, () => { });

        AggressiveDispatch<BlkPing, string>.SetEligibility(services, eligible: false);

        // Returning to plain Ineligible would let this grant arming again; Disarmed is terminal.
        AggressiveDispatch<BlkPing, string>.SetEligibility(services, eligible: true);

        AggressiveDispatch<BlkPing, string>.ShouldAttemptArm.ShouldBeFalse(
            "arming is one-shot: a pair that stood down must never arm again in this process");

        AggressiveDispatchLatch.ResetForTests();
        AggressiveDispatch<BlkPing, string>.ResetForTests();
    }
}
