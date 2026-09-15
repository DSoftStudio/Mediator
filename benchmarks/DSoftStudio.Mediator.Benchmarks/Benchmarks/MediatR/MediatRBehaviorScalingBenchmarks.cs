// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

// ── Requests: one type per behavior count ─────────────────────────
// Distinct request types rather than one type registered in N containers, so each library's own
// per-type dispatch state stays independent. Mirrors DSoftBehaviorScalingBenchmarks exactly: the
// same depths, the same pass-through behaviors, the same warmup, so the four curves are comparable.
public record MRScale0 : MediatR.IRequest<int>;
public record MRScale1 : MediatR.IRequest<int>;
public record MRScale2 : MediatR.IRequest<int>;
public record MRScale3 : MediatR.IRequest<int>;
public record MRScale5 : MediatR.IRequest<int>;
public record MRScale8 : MediatR.IRequest<int>;

public sealed class MRScale0Handler : MediatR.IRequestHandler<MRScale0, int>
{ public Task<int> Handle(MRScale0 r, CancellationToken ct) => Task.FromResult(42); }
public sealed class MRScale1Handler : MediatR.IRequestHandler<MRScale1, int>
{ public Task<int> Handle(MRScale1 r, CancellationToken ct) => Task.FromResult(42); }
public sealed class MRScale2Handler : MediatR.IRequestHandler<MRScale2, int>
{ public Task<int> Handle(MRScale2 r, CancellationToken ct) => Task.FromResult(42); }
public sealed class MRScale3Handler : MediatR.IRequestHandler<MRScale3, int>
{ public Task<int> Handle(MRScale3 r, CancellationToken ct) => Task.FromResult(42); }
public sealed class MRScale5Handler : MediatR.IRequestHandler<MRScale5, int>
{ public Task<int> Handle(MRScale5 r, CancellationToken ct) => Task.FromResult(42); }
public sealed class MRScale8Handler : MediatR.IRequestHandler<MRScale8, int>
{ public Task<int> Handle(MRScale8 r, CancellationToken ct) => Task.FromResult(42); }

// ── Pass-through behaviors ────────────────────────────────────────
// Eight distinct types, cycled for the deeper chains. Each does nothing but call the next link, so
// the curve measures the pipeline machinery and nothing else.
public sealed class MRChain1<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain2<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain3<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain4<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain5<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain6<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain7<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}
public sealed class MRChain8<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => next(ct);
}


// ── Execution verification ────────────────────────────────────────
// Counting REGISTRATIONS is not enough, and that is not a hypothetical. DispatchR registers generic
// links happily and then never chains them: measured in isolation, registered=2 / executed=0. Its
// whole scaling curve was flat by construction and the registration count could not see it. So this
// pair proves the links RUN, using the same GENERIC shape the measured rows depend on, with five
// distinct types so a library that collapses duplicates by type shows up as a collapse.

public record MRScaleVerify0 : MediatR.IRequest<int>;
public sealed class MRScaleVerify0Handler : MediatR.IRequestHandler<MRScaleVerify0, int>
{ public Task<int> Handle(MRScaleVerify0 r, CancellationToken ct) => Task.FromResult(42); }
public record MRScaleVerify1 : MediatR.IRequest<int>;
public sealed class MRScaleVerify1Handler : MediatR.IRequestHandler<MRScaleVerify1, int>
{ public Task<int> Handle(MRScaleVerify1 r, CancellationToken ct) => Task.FromResult(42); }
public record MRScaleVerify2 : MediatR.IRequest<int>;
public sealed class MRScaleVerify2Handler : MediatR.IRequestHandler<MRScaleVerify2, int>
{ public Task<int> Handle(MRScaleVerify2 r, CancellationToken ct) => Task.FromResult(42); }
public record MRScaleVerify3 : MediatR.IRequest<int>;
public sealed class MRScaleVerify3Handler : MediatR.IRequestHandler<MRScaleVerify3, int>
{ public Task<int> Handle(MRScaleVerify3 r, CancellationToken ct) => Task.FromResult(42); }
public record MRScaleVerify5 : MediatR.IRequest<int>;
public sealed class MRScaleVerify5Handler : MediatR.IRequestHandler<MRScaleVerify5, int>
{ public Task<int> Handle(MRScaleVerify5 r, CancellationToken ct) => Task.FromResult(42); }
public record MRScaleVerify8 : MediatR.IRequest<int>;
public sealed class MRScaleVerify8Handler : MediatR.IRequestHandler<MRScaleVerify8, int>
{ public Task<int> Handle(MRScaleVerify8 r, CancellationToken ct) => Task.FromResult(42); }

internal static class MRScaleCounter
{
    public static int Count;
}

public sealed class MRScaleCount1<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount2<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount3<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount4<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount6<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount7<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount8<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
public sealed class MRScaleCount5<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest r, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref MRScaleCounter.Count); return next(ct); }
}
/// <summary>
/// MediatR behavior scaling: 0, 1, 2, 3, 5, 8, 16 and 32 pass-through behaviors.
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
public class MediatRBehaviorScalingBenchmarks
{
    private static readonly MRScale0 M0 = new();
    private static readonly MRScale1 M1 = new();
    private static readonly MRScale2 M2 = new();
    private static readonly MRScale3 M3 = new();
    private static readonly MRScale5 M5 = new();
    private static readonly MRScale8 M8 = new();

    private IServiceScope _scope = null!;
    private MediatR.IMediator _mediator = null!;
    private MRScale0Handler _direct = null!;

    [GlobalSetup]
    public void Setup()
    {
        _direct = new MRScale0Handler();

        var services = new ServiceCollection();
        // MediatR resolves an ILoggerFactory during registration and throws without one.
        services.AddLogging();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(MRScale0Handler).Assembly));

        // Scale0 deliberately gets no behaviors at all.

        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale1, int>), typeof(MRChain1<MRScale1, int>));

        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale2, int>), typeof(MRChain1<MRScale2, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale2, int>), typeof(MRChain2<MRScale2, int>));

        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale3, int>), typeof(MRChain1<MRScale3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale3, int>), typeof(MRChain2<MRScale3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale3, int>), typeof(MRChain3<MRScale3, int>));

        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale5, int>), typeof(MRChain1<MRScale5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale5, int>), typeof(MRChain2<MRScale5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale5, int>), typeof(MRChain3<MRScale5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale5, int>), typeof(MRChain4<MRScale5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale5, int>), typeof(MRChain5<MRScale5, int>));

        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain1<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain2<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain3<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain4<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain5<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain6<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain7<MRScale8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScale8, int>), typeof(MRChain8<MRScale8, int>));



        // One twin per measured depth. Verifying a SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the depth-3 row has three and the depth-8
        // row has eight. That is the gap DispatchR's flat curve walked through the first time.
        // The depth-0 twin must run ZERO: it catches a registration landing on the wrong pair.
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify1, int>), typeof(MRScaleCount1<MRScaleVerify1, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify2, int>), typeof(MRScaleCount1<MRScaleVerify2, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify2, int>), typeof(MRScaleCount2<MRScaleVerify2, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify3, int>), typeof(MRScaleCount1<MRScaleVerify3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify3, int>), typeof(MRScaleCount2<MRScaleVerify3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify3, int>), typeof(MRScaleCount3<MRScaleVerify3, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify5, int>), typeof(MRScaleCount1<MRScaleVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify5, int>), typeof(MRScaleCount2<MRScaleVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify5, int>), typeof(MRScaleCount3<MRScaleVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify5, int>), typeof(MRScaleCount4<MRScaleVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify5, int>), typeof(MRScaleCount5<MRScaleVerify5, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount1<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount2<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount3<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount4<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount5<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount6<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount7<MRScaleVerify8, int>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<MRScaleVerify8, int>), typeof(MRScaleCount8<MRScaleVerify8, int>));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

        // ── Verify the chains really are the length we think ──
        // A silent misconfiguration would flatten the curve and we would "conclude" that behaviors
        // are free. Counting through DI costs nothing at measurement time.
        var sp = _scope.ServiceProvider;
        var n0 = sp.GetServices<MediatR.IPipelineBehavior<MRScale0, int>>().Count();
        var n1 = sp.GetServices<MediatR.IPipelineBehavior<MRScale1, int>>().Count();
        var n2 = sp.GetServices<MediatR.IPipelineBehavior<MRScale2, int>>().Count();
        var n3 = sp.GetServices<MediatR.IPipelineBehavior<MRScale3, int>>().Count();
        var n5 = sp.GetServices<MediatR.IPipelineBehavior<MRScale5, int>>().Count();
        var n8 = sp.GetServices<MediatR.IPipelineBehavior<MRScale8, int>>().Count();

        // Counting through DI costs nothing at measurement time, and a silent misconfiguration
        // would flatten the curve into "behaviors are free". These throw rather than print: a
        // banner in a twenty-minute run is read by nobody, and BenchmarkDotNet would report the
        // suite as successful either way.
        BenchmarkVerification.RequireCount(n0, 0, "MediatR scaling", "the depth-0 behavior chain");
        BenchmarkVerification.RequireCount(n1, 1, "MediatR scaling", "the depth-1 behavior chain");
        BenchmarkVerification.RequireCount(n2, 2, "MediatR scaling", "the depth-2 behavior chain");
        BenchmarkVerification.RequireCount(n3, 3, "MediatR scaling", "the depth-3 behavior chain");
        BenchmarkVerification.RequireCount(n5, 5, "MediatR scaling", "the depth-5 behavior chain");
        BenchmarkVerification.RequireCount(n8, 8, "MediatR scaling", "the depth-8 behavior chain");

        // And they RUN, not merely resolve.
        MRScaleCounter.Count = 0;
        var verify0 = _mediator.Send(new MRScaleVerify0()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRScaleCounter.Count, 0, "MediatR scaling", "the depth-0 chain, RUNNING");
        BenchmarkVerification.Require(
            verify0 == 42, "MediatR scaling", "the depth-0 twin returned " + verify0);
        MRScaleCounter.Count = 0;
        var verify1 = _mediator.Send(new MRScaleVerify1()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRScaleCounter.Count, 1, "MediatR scaling", "the depth-1 chain, RUNNING");
        BenchmarkVerification.Require(
            verify1 == 42, "MediatR scaling", "the depth-1 twin returned " + verify1);
        MRScaleCounter.Count = 0;
        var verify2 = _mediator.Send(new MRScaleVerify2()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRScaleCounter.Count, 2, "MediatR scaling", "the depth-2 chain, RUNNING");
        BenchmarkVerification.Require(
            verify2 == 42, "MediatR scaling", "the depth-2 twin returned " + verify2);
        MRScaleCounter.Count = 0;
        var verify3 = _mediator.Send(new MRScaleVerify3()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRScaleCounter.Count, 3, "MediatR scaling", "the depth-3 chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "MediatR scaling", "the depth-3 twin returned " + verify3);
        MRScaleCounter.Count = 0;
        var verify5 = _mediator.Send(new MRScaleVerify5()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRScaleCounter.Count, 5, "MediatR scaling", "the depth-5 chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "MediatR scaling", "the depth-5 twin returned " + verify5);
        MRScaleCounter.Count = 0;
        var verify8 = _mediator.Send(new MRScaleVerify8()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            MRScaleCounter.Count, 8, "MediatR scaling", "the depth-8 chain, RUNNING");
        BenchmarkVerification.Require(
            verify8 == 42, "MediatR scaling", "the depth-8 twin returned " + verify8);

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
