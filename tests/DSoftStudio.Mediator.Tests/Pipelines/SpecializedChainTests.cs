// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Dedicated types: the registries are process-global statics ─────

public sealed record ChainSpecPing : IRequest<int>;

public sealed class ChainSpecPingHandler : IRequestHandler<ChainSpecPing, int>
{
    private readonly List<string> _log;
    public ChainSpecPingHandler(List<string> log) => _log = log;

    public ValueTask<int> Handle(ChainSpecPing request, CancellationToken ct)
    {
        _log.Add("handler");
        return new ValueTask<int>(9);
    }
}

public sealed class SpecA : IPipelineBehavior<ChainSpecPing, int>
{
    private readonly List<string> _log;
    public SpecA(List<string> log) => _log = log;

    public async ValueTask<int> Handle(
        ChainSpecPing r, IRequestHandler<ChainSpecPing, int> next, CancellationToken ct)
    { _log.Add("A"); return await next.Handle(r, ct); }
}

public sealed class SpecB : IPipelineBehavior<ChainSpecPing, int>
{
    private readonly List<string> _log;
    public SpecB(List<string> log) => _log = log;

    public async ValueTask<int> Handle(
        ChainSpecPing r, IRequestHandler<ChainSpecPing, int> next, CancellationToken ct)
    { _log.Add("B"); return await next.Handle(r, ct); }
}

public sealed class SpecC : IPipelineBehavior<ChainSpecPing, int>
{
    private readonly List<string> _log;
    public SpecC(List<string> log) => _log = log;

    public async ValueTask<int> Handle(
        ChainSpecPing r, IRequestHandler<ChainSpecPing, int> next, CancellationToken ct)
    { _log.Add("C"); return await next.Handle(r, ct); }
}

// ── Hand-written stand-ins for what the generator will emit ────────
// Every field is typed to a concrete class: the behavior AND the next link. That is the whole
// point of the tier — it is what lets the JIT bind and inline the entire chain.

file sealed class SpecL2 : IRequestHandler<ChainSpecPing, int>
{
    private readonly SpecC _b;
    private readonly IRequestHandler<ChainSpecPing, int> _next;   // terminal: the handler
    public SpecL2(SpecC b, IRequestHandler<ChainSpecPing, int> next) { _b = b; _next = next; }
    public ValueTask<int> Handle(ChainSpecPing r, CancellationToken ct) => _b.Handle(r, _next, ct);
}

file sealed class SpecL1 : IRequestHandler<ChainSpecPing, int>
{
    private readonly SpecB _b;
    private readonly SpecL2 _next;
    public SpecL1(SpecB b, SpecL2 next) { _b = b; _next = next; }
    public ValueTask<int> Handle(ChainSpecPing r, CancellationToken ct) => _b.Handle(r, _next, ct);
}

file sealed class SpecL0 : IRequestHandler<ChainSpecPing, int>
{
    private readonly SpecA _b;
    private readonly SpecL1 _next;
    public SpecL0(SpecA b, SpecL1 next) { _b = b; _next = next; }
    public ValueTask<int> Handle(ChainSpecPing r, CancellationToken ct) => _b.Handle(r, _next, ct);
}

/// <summary>
/// Runtime contract for the fully specialized chain tier (BehaviorChainRegistry), exercised with
/// hand-written links that stand in for the generator's output.
/// <para>
/// The generator predicts the chain from registration sites, which cannot see conditional
/// registration, factory lambdas, or registrations inside a referenced assembly's method body. So the
/// prediction has to be verified against the RESOLVED INSTANCES, and a wrong prediction must cost
/// performance and never correctness. These tests pin exactly that.
/// </para>
/// </summary>
public class SpecializedChainTests
{
    // Instance fields, not statics: xUnit builds a fresh instance per test, so two tests can never
    // see each other's counts even if they were ever run concurrently. The registry itself stays
    // process-global by design - it mirrors the real runtime - so each test still resets it.
    private int _attempts;
    private int _matches;

    /// <summary>
    /// The shape the generator will emit: length check, then an EXACT type check per position, then
    /// construction with concrete-typed fields. Any mismatch returns null so the caller falls back.
    /// </summary>
    private IRequestHandler<ChainSpecPing, int>? PredictedAbc(
        IPipelineBehavior<ChainSpecPing, int>[] behaviors,
        IRequestHandler<ChainSpecPing, int> handler)
    {
        _attempts++;

        if (behaviors.Length != 3) return null;
        if (behaviors[0].GetType() != typeof(SpecA)) return null;
        if (behaviors[1].GetType() != typeof(SpecB)) return null;
        if (behaviors[2].GetType() != typeof(SpecC)) return null;

        _matches++;

        return new SpecL0(
            (SpecA)behaviors[0],
            new SpecL1(
                (SpecB)behaviors[1],
                new SpecL2((SpecC)behaviors[2], handler)));
    }

    private static ServiceProvider BuildProvider(List<string> log, params Type[] behaviorsInOrder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddTransient<IRequestHandler<ChainSpecPing, int>, ChainSpecPingHandler>();

        foreach (var t in behaviorsInOrder)
            services.AddTransient(typeof(IPipelineBehavior<ChainSpecPing, int>), t);

        services.AddTransient<PipelineChainHandler<ChainSpecPing, int>>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PredictedChain_Matches_UsesSpecializedChain_AndPreservesOrder()
    {
        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();
        _attempts = 0; _matches = 0;
        BehaviorChainRegistry<ChainSpecPing, int>.Register(PredictedAbc);

        var log = new List<string>();
        await using var sp = BuildProvider(log, typeof(SpecA), typeof(SpecB), typeof(SpecC));

        var chain = sp.GetRequiredService<PipelineChainHandler<ChainSpecPing, int>>();
        var result = await chain.Handle(new ChainSpecPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(9);
        _attempts.ShouldBe(1);
        _matches.ShouldBe(1, "the resolved instances match the prediction, so the specialized chain must be used");
        log.ShouldBe(new[] { "A", "B", "C", "handler" });

        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();
    }

    [Fact]
    public async Task PredictedChain_WrongOrder_FallsBack_AndStillRunsRegistrationOrder()
    {
        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();
        _attempts = 0; _matches = 0;
        BehaviorChainRegistry<ChainSpecPing, int>.Register(PredictedAbc);

        // Registered C, A, B — the generator predicted A, B, C. The prediction is WRONG.
        var log = new List<string>();
        await using var sp = BuildProvider(log, typeof(SpecC), typeof(SpecA), typeof(SpecB));

        var chain = sp.GetRequiredService<PipelineChainHandler<ChainSpecPing, int>>();
        var result = await chain.Handle(new ChainSpecPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(9);
        _attempts.ShouldBe(1);
        _matches.ShouldBe(0, "a mismatched prediction must be rejected, not used");

        // And the fallback runs the behaviors in REGISTRATION order, not the predicted one.
        log.ShouldBe(new[] { "C", "A", "B", "handler" });

        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();
    }

    [Fact]
    public async Task PredictedChain_WrongLength_FallsBack()
    {
        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();
        _attempts = 0; _matches = 0;
        BehaviorChainRegistry<ChainSpecPing, int>.Register(PredictedAbc);

        var log = new List<string>();
        await using var sp = BuildProvider(log, typeof(SpecA), typeof(SpecB));

        var chain = sp.GetRequiredService<PipelineChainHandler<ChainSpecPing, int>>();
        var result = await chain.Handle(new ChainSpecPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(9);
        _matches.ShouldBe(0, "a chain shorter than the prediction must be rejected");
        log.ShouldBe(new[] { "A", "B", "handler" });

        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();
    }

    [Fact]
    public async Task NoPredictionRegistered_BehavesExactlyAsBefore()
    {
        BehaviorChainRegistry<ChainSpecPing, int>.ResetForTests();

        var log = new List<string>();
        await using var sp = BuildProvider(log, typeof(SpecA), typeof(SpecB), typeof(SpecC));

        var chain = sp.GetRequiredService<PipelineChainHandler<ChainSpecPing, int>>();
        var result = await chain.Handle(new ChainSpecPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(9);
        log.ShouldBe(new[] { "A", "B", "C", "handler" });
    }
}
