// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

// ── Requests: one type per behavior count ─────────────────────────
// Distinct request types rather than one type registered in N containers, so each library's own
// per-type dispatch state stays independent. Mirrors DSoftBehaviorScalingBenchmarks exactly: the
// same depths, the same pass-through behaviors, the same warmup, so the four curves are comparable.
public sealed record SGScale0 : global::Mediator.IRequest<int>;
public sealed record SGScale1 : global::Mediator.IRequest<int>;
public sealed record SGScale2 : global::Mediator.IRequest<int>;
public sealed record SGScale3 : global::Mediator.IRequest<int>;
public sealed record SGScale5 : global::Mediator.IRequest<int>;
public sealed record SGScale8 : global::Mediator.IRequest<int>;

public sealed class SGScale0Handler : global::Mediator.IRequestHandler<SGScale0, int>
{ public ValueTask<int> Handle(SGScale0 r, CancellationToken ct) => new(42); }
public sealed class SGScale1Handler : global::Mediator.IRequestHandler<SGScale1, int>
{ public ValueTask<int> Handle(SGScale1 r, CancellationToken ct) => new(42); }
public sealed class SGScale2Handler : global::Mediator.IRequestHandler<SGScale2, int>
{ public ValueTask<int> Handle(SGScale2 r, CancellationToken ct) => new(42); }
public sealed class SGScale3Handler : global::Mediator.IRequestHandler<SGScale3, int>
{ public ValueTask<int> Handle(SGScale3 r, CancellationToken ct) => new(42); }
public sealed class SGScale5Handler : global::Mediator.IRequestHandler<SGScale5, int>
{ public ValueTask<int> Handle(SGScale5 r, CancellationToken ct) => new(42); }
public sealed class SGScale8Handler : global::Mediator.IRequestHandler<SGScale8, int>
{ public ValueTask<int> Handle(SGScale8 r, CancellationToken ct) => new(42); }

// ── Pass-through behaviors ────────────────────────────────────────
// Eight distinct types, cycled for the deeper chains. Each does nothing but call the next link, so
// the curve measures the pipeline machinery and nothing else.
public sealed class SGChain1<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain2<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain3<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain4<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain5<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain6<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain7<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}
public sealed class SGChain8<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
        => next(message, ct);
}


// ── Execution verification ────────────────────────────────────────
// Counting REGISTRATIONS is not enough, and that is not a hypothetical. DispatchR registers generic
// links happily and then never chains them: measured in isolation, registered=2 / executed=0. Its
// whole scaling curve was flat by construction and the registration count could not see it. So this
// pair proves the links RUN, using the same GENERIC shape the measured rows depend on, with five
// distinct types so a library that collapses duplicates by type shows up as a collapse.

public sealed record SGScaleVerify0 : global::Mediator.IRequest<int>;
public sealed class SGScaleVerify0Handler : global::Mediator.IRequestHandler<SGScaleVerify0, int>
{ public ValueTask<int> Handle(SGScaleVerify0 r, CancellationToken ct) => new(42); }
public sealed record SGScaleVerify1 : global::Mediator.IRequest<int>;
public sealed class SGScaleVerify1Handler : global::Mediator.IRequestHandler<SGScaleVerify1, int>
{ public ValueTask<int> Handle(SGScaleVerify1 r, CancellationToken ct) => new(42); }
public sealed record SGScaleVerify2 : global::Mediator.IRequest<int>;
public sealed class SGScaleVerify2Handler : global::Mediator.IRequestHandler<SGScaleVerify2, int>
{ public ValueTask<int> Handle(SGScaleVerify2 r, CancellationToken ct) => new(42); }
public sealed record SGScaleVerify3 : global::Mediator.IRequest<int>;
public sealed class SGScaleVerify3Handler : global::Mediator.IRequestHandler<SGScaleVerify3, int>
{ public ValueTask<int> Handle(SGScaleVerify3 r, CancellationToken ct) => new(42); }
public sealed record SGScaleVerify5 : global::Mediator.IRequest<int>;
public sealed class SGScaleVerify5Handler : global::Mediator.IRequestHandler<SGScaleVerify5, int>
{ public ValueTask<int> Handle(SGScaleVerify5 r, CancellationToken ct) => new(42); }
public sealed record SGScaleVerify8 : global::Mediator.IRequest<int>;
public sealed class SGScaleVerify8Handler : global::Mediator.IRequestHandler<SGScaleVerify8, int>
{ public ValueTask<int> Handle(SGScaleVerify8 r, CancellationToken ct) => new(42); }

internal static class SGScaleCounter
{
    public static int Count;
}

public sealed class SGScaleCount1<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount2<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount3<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount4<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount6<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount7<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount8<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
public sealed class SGScaleCount5<TMessage, TResponse> : global::Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : global::Mediator.IMessage
{
    public ValueTask<TResponse> Handle(TMessage message,
        global::Mediator.MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref SGScaleCounter.Count); return next(message, ct); }
}
/// <summary>
/// Mediator (source-generated) behavior scaling: 0, 1, 2, 3, 5, 8, 16 and 32 pass-through behaviors.
/// <para>
/// Warmup is 12 iterations, not the default: a deeper chain is more methods to tier up, and with the
/// default the deep rows were measured mid-tiering while the shallow ones had settled. The median
/// column is always on because a row that catches interference shows a mean far from its median.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 12, iterationCount: 30)]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
public class MediatorSGBehaviorScalingBenchmarks
{
    private static readonly SGScale0 M0 = new();
    private static readonly SGScale1 M1 = new();
    private static readonly SGScale2 M2 = new();
    private static readonly SGScale3 M3 = new();
    private static readonly SGScale5 M5 = new();
    private static readonly SGScale8 M8 = new();

    private IServiceScope _scope = null!;
    private global::Mediator.IMediator _mediator = null!;
    private SGScale0Handler _direct = null!;

    [GlobalSetup]
    public void Setup()
    {
        _direct = new SGScale0Handler();

        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        // Scale0 deliberately gets no behaviors at all.

        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale1, int>), typeof(SGChain1<SGScale1, int>));

        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale2, int>), typeof(SGChain1<SGScale2, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale2, int>), typeof(SGChain2<SGScale2, int>));

        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale3, int>), typeof(SGChain1<SGScale3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale3, int>), typeof(SGChain2<SGScale3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale3, int>), typeof(SGChain3<SGScale3, int>));

        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale5, int>), typeof(SGChain1<SGScale5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale5, int>), typeof(SGChain2<SGScale5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale5, int>), typeof(SGChain3<SGScale5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale5, int>), typeof(SGChain4<SGScale5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale5, int>), typeof(SGChain5<SGScale5, int>));

        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain1<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain2<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain3<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain4<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain5<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain6<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain7<SGScale8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScale8, int>), typeof(SGChain8<SGScale8, int>));



        // One twin per measured depth. Verifying a SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the depth-3 row has three and the depth-8
        // row has eight. That is the gap DispatchR's flat curve walked through the first time.
        // The depth-0 twin must run ZERO: it catches a registration landing on the wrong pair.
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify1, int>), typeof(SGScaleCount1<SGScaleVerify1, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify2, int>), typeof(SGScaleCount1<SGScaleVerify2, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify2, int>), typeof(SGScaleCount2<SGScaleVerify2, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify3, int>), typeof(SGScaleCount1<SGScaleVerify3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify3, int>), typeof(SGScaleCount2<SGScaleVerify3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify3, int>), typeof(SGScaleCount3<SGScaleVerify3, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify5, int>), typeof(SGScaleCount1<SGScaleVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify5, int>), typeof(SGScaleCount2<SGScaleVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify5, int>), typeof(SGScaleCount3<SGScaleVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify5, int>), typeof(SGScaleCount4<SGScaleVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify5, int>), typeof(SGScaleCount5<SGScaleVerify5, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount1<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount2<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount3<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount4<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount5<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount6<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount7<SGScaleVerify8, int>));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<SGScaleVerify8, int>), typeof(SGScaleCount8<SGScaleVerify8, int>));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();

        // ── Verify the chains really are the length we think ──
        // A silent misconfiguration would flatten the curve and we would "conclude" that behaviors
        // are free. Counting through DI costs nothing at measurement time.
        var sp = _scope.ServiceProvider;
        var n0 = sp.GetServices<global::Mediator.IPipelineBehavior<SGScale0, int>>().Count();
        var n1 = sp.GetServices<global::Mediator.IPipelineBehavior<SGScale1, int>>().Count();
        var n2 = sp.GetServices<global::Mediator.IPipelineBehavior<SGScale2, int>>().Count();
        var n3 = sp.GetServices<global::Mediator.IPipelineBehavior<SGScale3, int>>().Count();
        var n5 = sp.GetServices<global::Mediator.IPipelineBehavior<SGScale5, int>>().Count();
        var n8 = sp.GetServices<global::Mediator.IPipelineBehavior<SGScale8, int>>().Count();

        // Counting through DI costs nothing at measurement time, and a silent misconfiguration
        // would flatten the curve into "behaviors are free". These throw rather than print: a
        // banner in a twenty-minute run is read by nobody, and BenchmarkDotNet would report the
        // suite as successful either way.
        BenchmarkVerification.RequireCount(n0, 0, "Mediator (SG) scaling", "the depth-0 behavior chain");
        BenchmarkVerification.RequireCount(n1, 1, "Mediator (SG) scaling", "the depth-1 behavior chain");
        BenchmarkVerification.RequireCount(n2, 2, "Mediator (SG) scaling", "the depth-2 behavior chain");
        BenchmarkVerification.RequireCount(n3, 3, "Mediator (SG) scaling", "the depth-3 behavior chain");
        BenchmarkVerification.RequireCount(n5, 5, "Mediator (SG) scaling", "the depth-5 behavior chain");
        BenchmarkVerification.RequireCount(n8, 8, "Mediator (SG) scaling", "the depth-8 behavior chain");

        // And they RUN, not merely resolve.
        SGScaleCounter.Count = 0;
        var verify0 = _mediator.Send(new SGScaleVerify0()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGScaleCounter.Count, 0, "Mediator (SG) scaling", "the depth-0 chain, RUNNING");
        BenchmarkVerification.Require(
            verify0 == 42, "Mediator (SG) scaling", "the depth-0 twin returned " + verify0);
        SGScaleCounter.Count = 0;
        var verify1 = _mediator.Send(new SGScaleVerify1()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGScaleCounter.Count, 1, "Mediator (SG) scaling", "the depth-1 chain, RUNNING");
        BenchmarkVerification.Require(
            verify1 == 42, "Mediator (SG) scaling", "the depth-1 twin returned " + verify1);
        SGScaleCounter.Count = 0;
        var verify2 = _mediator.Send(new SGScaleVerify2()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGScaleCounter.Count, 2, "Mediator (SG) scaling", "the depth-2 chain, RUNNING");
        BenchmarkVerification.Require(
            verify2 == 42, "Mediator (SG) scaling", "the depth-2 twin returned " + verify2);
        SGScaleCounter.Count = 0;
        var verify3 = _mediator.Send(new SGScaleVerify3()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGScaleCounter.Count, 3, "Mediator (SG) scaling", "the depth-3 chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "Mediator (SG) scaling", "the depth-3 twin returned " + verify3);
        SGScaleCounter.Count = 0;
        var verify5 = _mediator.Send(new SGScaleVerify5()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGScaleCounter.Count, 5, "Mediator (SG) scaling", "the depth-5 chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "Mediator (SG) scaling", "the depth-5 twin returned " + verify5);
        SGScaleCounter.Count = 0;
        var verify8 = _mediator.Send(new SGScaleVerify8()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            SGScaleCounter.Count, 8, "Mediator (SG) scaling", "the depth-8 chain, RUNNING");
        BenchmarkVerification.Require(
            verify8 == 42, "Mediator (SG) scaling", "the depth-8 twin returned " + verify8);

        // Warm every path.
        _mediator.Send(M0).GetAwaiter().GetResult();
        _mediator.Send(M1).GetAwaiter().GetResult();
        _mediator.Send(M2).GetAwaiter().GetResult();
        _mediator.Send(M3).GetAwaiter().GetResult();
        _mediator.Send(M5).GetAwaiter().GetResult();
        _mediator.Send(M8).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall() => await _direct.Handle(M0, default);

    [Benchmark] public async Task<int> Send_0Behaviors() => await _mediator.Send(M0);
    [Benchmark] public async Task<int> Send_1Behaviors() => await _mediator.Send(M1);
    [Benchmark] public async Task<int> Send_2Behaviors() => await _mediator.Send(M2);
    [Benchmark] public async Task<int> Send_3Behaviors() => await _mediator.Send(M3);
    [Benchmark] public async Task<int> Send_5Behaviors() => await _mediator.Send(M5);
    [Benchmark] public async Task<int> Send_8Behaviors() => await _mediator.Send(M8);
}
