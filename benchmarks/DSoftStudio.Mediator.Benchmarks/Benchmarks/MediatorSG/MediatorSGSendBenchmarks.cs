// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

// ── One request type per depth ────────────────────────────────────
// A single request reused across depths forces a CONTAINER PER DEPTH, because one pair can only have
// one chain. Distinct pairs put both depths in ONE container — the shape an application actually has,
// and the shape MediatorSGBehaviorScalingBenchmarks uses, which is what makes these rows contrastable
// with that curve's 3- and 5-points.
public sealed record SGSend3 : global::Mediator.IRequest<int>;
public sealed record SGSend5 : global::Mediator.IRequest<int>;
public sealed record SGSendVerify3 : global::Mediator.IRequest<int>;
public sealed record SGSendVerify5 : global::Mediator.IRequest<int>;

public sealed class SGSend3Handler : global::Mediator.IRequestHandler<SGSend3, int>
{ public ValueTask<int> Handle(SGSend3 r, CancellationToken ct) => new(42); }

public sealed class SGSend5Handler : global::Mediator.IRequestHandler<SGSend5, int>
{ public ValueTask<int> Handle(SGSend5 r, CancellationToken ct) => new(42); }

public sealed class SGSendVerify3Handler : global::Mediator.IRequestHandler<SGSendVerify3, int>
{ public ValueTask<int> Handle(SGSendVerify3 r, CancellationToken ct) => new(42); }
public sealed class SGSendVerify5Handler : global::Mediator.IRequestHandler<SGSendVerify5, int>
{ public ValueTask<int> Handle(SGSendVerify5 r, CancellationToken ct) => new(42); }

// ── Counting links, for the VERIFICATION pair only ────────────────
// PUBLIC and top-level, not private nested, and that is load-bearing: the generator cannot name a
// private nested type, so it emitted NO specialized chain for this pair and the verification ran on
// the fallback adapter instead of the shape every measured row uses. Confirmed in the emitted
// registry — the verify pair's footprint matched a pair with zero behaviors.
// Five distinct types, so a library that collapses duplicates by type shows up as a collapse.

internal static class SGSendCounter
{
    public static int Count;
}

public sealed class SGSendCount1<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGSendCounter.Count); return next(message, ct); }
}
public sealed class SGSendCount2<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGSendCounter.Count); return next(message, ct); }
}
public sealed class SGSendCount3<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGSendCounter.Count); return next(message, ct); }
}
public sealed class SGSendCount4<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGSendCounter.Count); return next(message, ct); }
}
public sealed class SGSendCount5<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGSendCounter.Count); return next(message, ct); }
}

/// <summary>
/// Isolated Mediator (source-generated) benchmark: Send with 3 / 5 behaviors.
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
public class MediatorSGSendBenchmarks
{
    private static readonly SGSend3 Message3 = new();
    private static readonly SGSend5 Message5 = new();

    private global::Mediator.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        // The SAME pass-through types the scaling suite chains, so "five behaviors" means the same
        // thing in both places by construction. Singleton is this library's own convention.
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend3, int>), typeof(SGChain1<SGSend3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend3, int>), typeof(SGChain2<SGSend3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend3, int>), typeof(SGChain3<SGSend3, int>));

        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend5, int>), typeof(SGChain1<SGSend5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend5, int>), typeof(SGChain2<SGSend5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend5, int>), typeof(SGChain3<SGSend5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend5, int>), typeof(SGChain4<SGSend5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSend5, int>), typeof(SGChain5<SGSend5, int>));

        // Counting links on their OWN pairs, so execution is proved without a second container
        // and without a counter anywhere near a measured row.
        // One twin per measured depth, 3 and 5. A SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the 3-behavior row really has three.
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify3, int>), typeof(SGSendCount1<SGSendVerify3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify3, int>), typeof(SGSendCount2<SGSendVerify3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify3, int>), typeof(SGSendCount3<SGSendVerify3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify5, int>), typeof(SGSendCount1<SGSendVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify5, int>), typeof(SGSendCount2<SGSendVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify5, int>), typeof(SGSendCount3<SGSendVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify5, int>), typeof(SGSendCount4<SGSendVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGSendVerify5, int>), typeof(SGSendCount5<SGSendVerify5, int>));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();

        // The chains are the length we think.
        var sp = _scope.ServiceProvider;
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::Mediator.IPipelineBehavior<SGSend3, int>>().Count(), 3,
            "Mediator (SG) Send", "the 3-behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::Mediator.IPipelineBehavior<SGSend5, int>>().Count(), 5,
            "Mediator (SG) Send", "the 5-behavior chain");

        // And they RUN, not merely resolve.
        SGSendCounter.Count = 0;
        var verify3 = _mediator.Send(new SGSendVerify3()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGSendCounter.Count, 3, "Mediator (SG) Send", "the 3-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "Mediator (SG) Send", "the 3-behavior twin returned " + verify3);
        SGSendCounter.Count = 0;
        var verify5 = _mediator.Send(new SGSendVerify5()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGSendCounter.Count, 5, "Mediator (SG) Send", "the 5-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "Mediator (SG) Send", "the 5-behavior twin returned " + verify5);

        // Warmup.
        _mediator.Send(Message3).GetAwaiter().GetResult();
        _mediator.Send(Message5).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall()
        => await new SGSend3Handler().Handle(Message3, default);

    [Benchmark]
    public async Task<int> MediatorSG_Send_3Behaviors()
        => await _mediator.Send(Message3);

    [Benchmark]
    public async Task<int> MediatorSG_Send_5Behaviors()
        => await _mediator.Send(Message5);
}
