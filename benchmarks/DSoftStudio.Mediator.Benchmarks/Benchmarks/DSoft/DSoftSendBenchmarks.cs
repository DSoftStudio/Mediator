// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

// ── One request type per depth ────────────────────────────────────
// A single request reused across depths forces a CONTAINER PER DEPTH, because one pair can only have
// one chain. That shape is not what an application looks like, and for this library it also meant
// sharing write-once dispatch statics between containers — the old file had to build the 5-behavior
// container first and said so in a comment. Distinct pairs put every depth in ONE container, remove
// the ordering constraint, and make these rows directly contrastable with DSoftBehaviorScalingBenchmarks,
// which is built the same way.
public sealed record DSoftSend3 : IRequest<int>;
public sealed record DSoftSend5 : IRequest<int>;
public sealed record DSoftSendVerify3 : IRequest<int>;
public sealed record DSoftSendVerify5 : IRequest<int>;

public sealed class DSoftSend3Handler : IRequestHandler<DSoftSend3, int>
{ public ValueTask<int> Handle(DSoftSend3 r, CancellationToken ct) => new(42); }

public sealed class DSoftSend5Handler : IRequestHandler<DSoftSend5, int>
{ public ValueTask<int> Handle(DSoftSend5 r, CancellationToken ct) => new(42); }

public sealed class DSoftSendVerify3Handler : IRequestHandler<DSoftSendVerify3, int>
{ public ValueTask<int> Handle(DSoftSendVerify3 r, CancellationToken ct) => new(42); }
public sealed class DSoftSendVerify5Handler : IRequestHandler<DSoftSendVerify5, int>
{ public ValueTask<int> Handle(DSoftSendVerify5 r, CancellationToken ct) => new(42); }

// ── Counting links, for the VERIFICATION pair only ────────────────
// PUBLIC and top-level, not private nested, and that is load-bearing: the generator cannot name a
// private nested type, so it emitted NO specialized chain for this pair and the verification ran on
// the fallback adapter instead of the shape every measured row uses. Confirmed in the emitted
// registry — the verify pair's footprint matched a pair with zero behaviors.
// Five distinct types, so a library that collapses duplicates by type shows up as a collapse.

internal static class DSoftSendCounter
{
    public static int Count;
}

public sealed class DSoftSendCount1<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref DSoftSendCounter.Count); return next.Handle(r, ct); }
}
public sealed class DSoftSendCount2<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref DSoftSendCounter.Count); return next.Handle(r, ct); }
}
public sealed class DSoftSendCount3<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref DSoftSendCounter.Count); return next.Handle(r, ct); }
}
public sealed class DSoftSendCount4<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref DSoftSendCounter.Count); return next.Handle(r, ct); }
}
public sealed class DSoftSendCount5<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref DSoftSendCounter.Count); return next.Handle(r, ct); }
}

/// <summary>
/// Isolated DSoft-only benchmark: Send with 3 / 5 behaviors.
/// No other mediator libraries — avoids static dispatch table contamination.
/// </summary>
[MemoryDiagnoser]
// Same job AND same ordering as BehaviorScalingBenchmarks, so the 3- and 5-behavior rows of the two
// suites are directly contrastable. The default warmup already misled once: deep rows were measured
// mid-tiering, StdDev 2.86 falling to 0.02 with twelve warmup iterations.
[SimpleJob(warmupCount: 12, iterationCount: 30)]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
public class DSoftSendBenchmarks
{
    private static readonly DSoftSend3 Message3 = new();
    private static readonly DSoftSend5 Message5 = new();

    private PingHandler _directHandler = null!;
    private IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingHandler();

        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers();

        // The SAME pass-through types the scaling suite chains, so "five behaviors" means the same
        // thing in both places by construction rather than by two definitions that happen to agree.
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend3, int>), typeof(Chain1<DSoftSend3, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend3, int>), typeof(Chain2<DSoftSend3, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend3, int>), typeof(Chain3<DSoftSend3, int>));

        services.AddScoped(typeof(IPipelineBehavior<DSoftSend5, int>), typeof(Chain1<DSoftSend5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend5, int>), typeof(Chain2<DSoftSend5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend5, int>), typeof(Chain3<DSoftSend5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend5, int>), typeof(Chain4<DSoftSend5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSend5, int>), typeof(Chain5<DSoftSend5, int>));

        // Counting links on their OWN pairs, so execution is proved without a second container
        // and without a counter anywhere near a measured row.
        // One twin per measured depth, 3 and 5. A SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the 3-behavior row really has three.
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify3, int>), typeof(DSoftSendCount1<DSoftSendVerify3, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify3, int>), typeof(DSoftSendCount2<DSoftSendVerify3, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify3, int>), typeof(DSoftSendCount3<DSoftSendVerify3, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify5, int>), typeof(DSoftSendCount1<DSoftSendVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify5, int>), typeof(DSoftSendCount2<DSoftSendVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify5, int>), typeof(DSoftSendCount3<DSoftSendVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify5, int>), typeof(DSoftSendCount4<DSoftSendVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<DSoftSendVerify5, int>), typeof(DSoftSendCount5<DSoftSendVerify5, int>));

        services.PrecompilePipelines();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();

        // The chains are the length we think.
        var sp = _scope.ServiceProvider;
        BenchmarkVerification.RequireCount(
            sp.GetServices<IPipelineBehavior<DSoftSend3, int>>().Count(), 3, "DSoft Send", "the 3-behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<IPipelineBehavior<DSoftSend5, int>>().Count(), 5, "DSoft Send", "the 5-behavior chain");

        // And they RUN, not merely resolve.
        DSoftSendCounter.Count = 0;
        var verify3 = _mediator.Send<DSoftSendVerify3, int>(new DSoftSendVerify3()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DSoftSendCounter.Count, 3, "DSoft Send", "the 3-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "DSoft Send", "the 3-behavior twin returned " + verify3);
        DSoftSendCounter.Count = 0;
        var verify5 = _mediator.Send<DSoftSendVerify5, int>(new DSoftSendVerify5()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DSoftSendCounter.Count, 5, "DSoft Send", "the 5-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "DSoft Send", "the 5-behavior twin returned " + verify5);

        // Warmup.
        _directHandler.Handle(new Ping(), default).GetAwaiter().GetResult();
        _mediator.Send<DSoftSend3, int>(Message3).GetAwaiter().GetResult();
        _mediator.Send<DSoftSend5, int>(Message5).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall()
        => await _directHandler.Handle(new Ping(), default);

    [Benchmark]
    public async Task<int> DSoft_Send_3Behaviors()
        => await _mediator.Send<DSoftSend3, int>(Message3);

    [Benchmark]
    public async Task<int> DSoft_Send_5Behaviors()
        => await _mediator.Send<DSoftSend5, int>(Message5);
}
