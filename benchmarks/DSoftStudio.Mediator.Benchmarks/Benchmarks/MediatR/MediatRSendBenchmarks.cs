// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

// ── One request type per depth ────────────────────────────────────
// A single request reused across depths forces a CONTAINER PER DEPTH, because one pair can only have
// one chain. Distinct pairs put both depths in ONE container — the shape an application actually has,
// and the shape MediatRBehaviorScalingBenchmarks uses, which is what makes these rows contrastable
// with that curve's 3- and 5-points.
public record MRSend3 : MediatR.IRequest<int>;
public record MRSend5 : MediatR.IRequest<int>;
public record MRSendVerify3 : MediatR.IRequest<int>;
public record MRSendVerify5 : MediatR.IRequest<int>;

public sealed class MRSend3Handler : MediatR.IRequestHandler<MRSend3, int>
{ public Task<int> Handle(MRSend3 r, CancellationToken ct) => Task.FromResult(42); }

public sealed class MRSend5Handler : MediatR.IRequestHandler<MRSend5, int>
{ public Task<int> Handle(MRSend5 r, CancellationToken ct) => Task.FromResult(42); }

public sealed class MRSendVerify3Handler : MediatR.IRequestHandler<MRSendVerify3, int>
{ public Task<int> Handle(MRSendVerify3 r, CancellationToken ct) => Task.FromResult(42); }
public sealed class MRSendVerify5Handler : MediatR.IRequestHandler<MRSendVerify5, int>
{ public Task<int> Handle(MRSendVerify5 r, CancellationToken ct) => Task.FromResult(42); }

// ── Counting links, for the VERIFICATION pair only ────────────────
// PUBLIC and top-level, not private nested, and that is load-bearing: the generator cannot name a
// private nested type, so it emitted NO specialized chain for this pair and the verification ran on
// the fallback adapter instead of the shape every measured row uses. Confirmed in the emitted
// registry — the verify pair's footprint matched a pair with zero behaviors.
// Five distinct types, so a library that collapses duplicates by type shows up as a collapse.

internal static class MRSendCounter
{
    public static int Count;
}

public sealed class MRSendCount1<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRSendCounter.Count); return next(ct); }
}
public sealed class MRSendCount2<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRSendCounter.Count); return next(ct); }
}
public sealed class MRSendCount3<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRSendCounter.Count); return next(ct); }
}
public sealed class MRSendCount4<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRSendCounter.Count); return next(ct); }
}
public sealed class MRSendCount5<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRSendCounter.Count); return next(ct); }
}

/// <summary>
/// Isolated MediatR-only benchmark: Send with 3 / 5 behaviors.
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
public class MediatRSendBenchmarks
{
    private static readonly MRSend3 Message3 = new();
    private static readonly MRSend5 Message5 = new();

    private MediatR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(MRSend3Handler).Assembly));

        // The SAME pass-through types the scaling suite chains, so "five behaviors" means the same
        // thing in both places by construction. Transient is MediatR's own registration convention.
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend3, int>), typeof(MRChain1<MRSend3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend3, int>), typeof(MRChain2<MRSend3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend3, int>), typeof(MRChain3<MRSend3, int>));

        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend5, int>), typeof(MRChain1<MRSend5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend5, int>), typeof(MRChain2<MRSend5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend5, int>), typeof(MRChain3<MRSend5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend5, int>), typeof(MRChain4<MRSend5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSend5, int>), typeof(MRChain5<MRSend5, int>));

        // Counting links on their OWN pairs, so execution is proved without a second container
        // and without a counter anywhere near a measured row.
        // One twin per measured depth, 3 and 5. A SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the 3-behavior row really has three.
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify3, int>), typeof(MRSendCount1<MRSendVerify3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify3, int>), typeof(MRSendCount2<MRSendVerify3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify3, int>), typeof(MRSendCount3<MRSendVerify3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify5, int>), typeof(MRSendCount1<MRSendVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify5, int>), typeof(MRSendCount2<MRSendVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify5, int>), typeof(MRSendCount3<MRSendVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify5, int>), typeof(MRSendCount4<MRSendVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRSendVerify5, int>), typeof(MRSendCount5<MRSendVerify5, int>));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

        // The chains are the length we think.
        var sp = _scope.ServiceProvider;
        BenchmarkVerification.RequireCount(
            sp.GetServices<MediatR.IPipelineBehavior<MRSend3, int>>().Count(), 3, "MediatR Send", "the 3-behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<MediatR.IPipelineBehavior<MRSend5, int>>().Count(), 5, "MediatR Send", "the 5-behavior chain");

        // And they RUN, not merely resolve.
        MRSendCounter.Count = 0;
        var verify3 = _mediator.Send(new MRSendVerify3()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRSendCounter.Count, 3, "MediatR Send", "the 3-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "MediatR Send", "the 3-behavior twin returned " + verify3);
        MRSendCounter.Count = 0;
        var verify5 = _mediator.Send(new MRSendVerify5()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRSendCounter.Count, 5, "MediatR Send", "the 5-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "MediatR Send", "the 5-behavior twin returned " + verify5);

        // Warmup.
        _mediator.Send(Message3).GetAwaiter().GetResult();
        _mediator.Send(Message5).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall()
        => await new MRSend3Handler().Handle(Message3, default);

    [Benchmark]
    public async Task<int> MediatR_Send_3Behaviors()
        => await _mediator.Send(Message3);

    [Benchmark]
    public async Task<int> MediatR_Send_5Behaviors()
        => await _mediator.Send(Message5);
}
