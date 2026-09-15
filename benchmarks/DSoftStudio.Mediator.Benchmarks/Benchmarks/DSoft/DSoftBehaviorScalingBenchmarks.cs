// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

// ── Requests: one type per behavior count ─────────────────────────
// Distinct request types (rather than one type registered in N containers) so every
// RequestDispatch<,> / PipelineChainCache<,> static stays independent and the ADR-0065
// second-container poison latch never fires. One container, like a real app.
public record Scale0 : IRequest<int>;
public record Scale1 : IRequest<int>;
public record Scale2 : IRequest<int>;
public record Scale3 : IRequest<int>;
public record Scale5 : IRequest<int>;
public record Scale8 : IRequest<int>;

public sealed class Scale0Handler : IRequestHandler<Scale0, int>
{ public ValueTask<int> Handle(Scale0 r, CancellationToken ct) => new(42); }
public sealed class Scale1Handler : IRequestHandler<Scale1, int>
{ public ValueTask<int> Handle(Scale1 r, CancellationToken ct) => new(42); }
public sealed class Scale2Handler : IRequestHandler<Scale2, int>
{ public ValueTask<int> Handle(Scale2 r, CancellationToken ct) => new(42); }
public sealed class Scale3Handler : IRequestHandler<Scale3, int>
{ public ValueTask<int> Handle(Scale3 r, CancellationToken ct) => new(42); }
public sealed class Scale5Handler : IRequestHandler<Scale5, int>
{ public ValueTask<int> Handle(Scale5 r, CancellationToken ct) => new(42); }
public sealed class Scale8Handler : IRequestHandler<Scale8, int>
{ public ValueTask<int> Handle(Scale8 r, CancellationToken ct) => new(42); }

// ── 8 DISTINCT pass-through behaviors ─────────────────────────────
// Distinct types on purpose: reusing a single type would make every link a monomorphic
// call site, letting the JIT devirtualize far better than in a real pipeline (where each
// behavior is a different class) and understating the true per-behavior cost.
public sealed class Chain1<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain2<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain3<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain4<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain5<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain6<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain7<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }
public sealed class Chain8<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{ public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct) => next.Handle(r, ct); }


// ── Execution verification ────────────────────────────────────────
// Counting REGISTRATIONS is not enough, and that is not a hypothetical. DispatchR registers generic
// links happily and then never chains them: measured in isolation, registered=2 / executed=0. Its
// whole scaling curve was flat by construction and the registration count could not see it. So this
// pair proves the links RUN, using the same GENERIC shape the measured rows depend on, with five
// distinct types so a library that collapses duplicates by type shows up as a collapse.

public sealed record ScaleVerify0 : IRequest<int>;
public sealed class ScaleVerify0Handler : IRequestHandler<ScaleVerify0, int>
{ public ValueTask<int> Handle(ScaleVerify0 r, CancellationToken ct) => new(42); }
public sealed record ScaleVerify1 : IRequest<int>;
public sealed class ScaleVerify1Handler : IRequestHandler<ScaleVerify1, int>
{ public ValueTask<int> Handle(ScaleVerify1 r, CancellationToken ct) => new(42); }
public sealed record ScaleVerify2 : IRequest<int>;
public sealed class ScaleVerify2Handler : IRequestHandler<ScaleVerify2, int>
{ public ValueTask<int> Handle(ScaleVerify2 r, CancellationToken ct) => new(42); }
public sealed record ScaleVerify3 : IRequest<int>;
public sealed class ScaleVerify3Handler : IRequestHandler<ScaleVerify3, int>
{ public ValueTask<int> Handle(ScaleVerify3 r, CancellationToken ct) => new(42); }
public sealed record ScaleVerify5 : IRequest<int>;
public sealed class ScaleVerify5Handler : IRequestHandler<ScaleVerify5, int>
{ public ValueTask<int> Handle(ScaleVerify5 r, CancellationToken ct) => new(42); }
public sealed record ScaleVerify8 : IRequest<int>;
public sealed class ScaleVerify8Handler : IRequestHandler<ScaleVerify8, int>
{ public ValueTask<int> Handle(ScaleVerify8 r, CancellationToken ct) => new(42); }

internal static class ScaleCounter
{
    public static int Count;
}

public sealed class ScaleCount1<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount2<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount3<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount4<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount6<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount7<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount8<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
public sealed class ScaleCount5<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest r, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    { Interlocked.Increment(ref ScaleCounter.Count); return next.Handle(r, ct); }
}
/// <summary>
/// Behavior-count scaling study.
/// <para>
/// The 3-vs-5 pair in <see cref="DSoftSendBenchmarks"/> cannot separate FIXED dispatch overhead
/// from the MARGINAL cost per behavior: solving two unknowns from two noisy points multiplies any
/// error by 3x/5x, and repeated runs of the identical configuration disagreed badly — the implied
/// fixed cost landed anywhere between 1.96 ns and 7.84 ns depending on the run.
/// </para>
/// <para>
/// This measures 0, 1, 2, 3, 5 and 8 behaviors so both terms come from a LINEAR REGRESSION over
/// six points instead of a two-point subtraction.
/// </para>
/// <para>
/// NOTE: Send_0Behaviors has NO pipeline chain, so it takes the concrete-cache dispatch path
/// rather than the chain path. Regress over 1..8 only, and read Send_0Behaviors as the separate
/// "no chain at all" data point.
/// </para>
/// </summary>
// Warmup is 12 iterations, not the default: a deeper chain is more methods to tier up, and with
// the default the deep rows were measured mid-tiering while the shallow ones had settled.
// The median column is always on because a row that catches interference shows a mean far from
// its median -- twice in four runs, on different rows, which is the machine and not the depth.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 12, iterationCount: 30)]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
public class DSoftBehaviorScalingBenchmarks
{
    private static readonly Scale0 M0 = new();
    private static readonly Scale1 M1 = new();
    private static readonly Scale2 M2 = new();
    private static readonly Scale3 M3 = new();
    private static readonly Scale5 M5 = new();
    private static readonly Scale8 M8 = new();

    private Scale0Handler _direct = null!;
    private IServiceScope _scope = null!;
    private IMediator _mediator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _direct = new Scale0Handler();

        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers();

        // Scale0 deliberately gets no behaviors at all.

        services.AddScoped(typeof(IPipelineBehavior<Scale1, int>), typeof(Chain1<Scale1, int>));

        services.AddScoped(typeof(IPipelineBehavior<Scale2, int>), typeof(Chain1<Scale2, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale2, int>), typeof(Chain2<Scale2, int>));

        services.AddScoped(typeof(IPipelineBehavior<Scale3, int>), typeof(Chain1<Scale3, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale3, int>), typeof(Chain2<Scale3, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale3, int>), typeof(Chain3<Scale3, int>));

        services.AddScoped(typeof(IPipelineBehavior<Scale5, int>), typeof(Chain1<Scale5, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale5, int>), typeof(Chain2<Scale5, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale5, int>), typeof(Chain3<Scale5, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale5, int>), typeof(Chain4<Scale5, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale5, int>), typeof(Chain5<Scale5, int>));

        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain1<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain2<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain3<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain4<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain5<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain6<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain7<Scale8, int>));
        services.AddScoped(typeof(IPipelineBehavior<Scale8, int>), typeof(Chain8<Scale8, int>));

        // One twin per measured depth. Verifying a SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the depth-3 row has three and the depth-8
        // row has eight. That is the gap DispatchR's flat curve walked through the first time.
        // The depth-0 twin must run ZERO: it catches a registration landing on the wrong pair.
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify1, int>), typeof(ScaleCount1<ScaleVerify1, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify2, int>), typeof(ScaleCount1<ScaleVerify2, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify2, int>), typeof(ScaleCount2<ScaleVerify2, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify3, int>), typeof(ScaleCount1<ScaleVerify3, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify3, int>), typeof(ScaleCount2<ScaleVerify3, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify3, int>), typeof(ScaleCount3<ScaleVerify3, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify5, int>), typeof(ScaleCount1<ScaleVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify5, int>), typeof(ScaleCount2<ScaleVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify5, int>), typeof(ScaleCount3<ScaleVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify5, int>), typeof(ScaleCount4<ScaleVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify5, int>), typeof(ScaleCount5<ScaleVerify5, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount1<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount2<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount3<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount4<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount5<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount6<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount7<ScaleVerify8, int>));
        services.AddScoped(typeof(IPipelineBehavior<ScaleVerify8, int>), typeof(ScaleCount8<ScaleVerify8, int>));

        services.PrecompilePipelines();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();

        // ── Verify the chains really are the length we think ──
        // A silent misconfiguration would flatten the curve and we would "conclude" that
        // behaviors are free. Counting through DI costs nothing at measurement time.
        var sp = _scope.ServiceProvider;
        var n0 = sp.GetServices<IPipelineBehavior<Scale0, int>>().Count();
        var n1 = sp.GetServices<IPipelineBehavior<Scale1, int>>().Count();
        var n2 = sp.GetServices<IPipelineBehavior<Scale2, int>>().Count();
        var n3 = sp.GetServices<IPipelineBehavior<Scale3, int>>().Count();
        var n5 = sp.GetServices<IPipelineBehavior<Scale5, int>>().Count();
        var n8 = sp.GetServices<IPipelineBehavior<Scale8, int>>().Count();

        // Counting through DI costs nothing at measurement time, and a silent misconfiguration
        // would flatten the curve into "behaviors are free". These throw rather than print: a
        // banner in a twenty-minute run is read by nobody, and BenchmarkDotNet would report the
        // suite as successful either way.
        BenchmarkVerification.RequireCount(n0, 0, "DSoft scaling", "the depth-0 behavior chain");
        BenchmarkVerification.RequireCount(n1, 1, "DSoft scaling", "the depth-1 behavior chain");
        BenchmarkVerification.RequireCount(n2, 2, "DSoft scaling", "the depth-2 behavior chain");
        BenchmarkVerification.RequireCount(n3, 3, "DSoft scaling", "the depth-3 behavior chain");
        BenchmarkVerification.RequireCount(n5, 5, "DSoft scaling", "the depth-5 behavior chain");
        BenchmarkVerification.RequireCount(n8, 8, "DSoft scaling", "the depth-8 behavior chain");

        // And they RUN, not merely resolve.
        ScaleCounter.Count = 0;
        var verify0 = _mediator.Send<ScaleVerify0, int>(new ScaleVerify0()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            ScaleCounter.Count, 0, "DSoft scaling", "the depth-0 chain, RUNNING");
        BenchmarkVerification.Require(
            verify0 == 42, "DSoft scaling", "the depth-0 twin returned " + verify0);
        ScaleCounter.Count = 0;
        var verify1 = _mediator.Send<ScaleVerify1, int>(new ScaleVerify1()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            ScaleCounter.Count, 1, "DSoft scaling", "the depth-1 chain, RUNNING");
        BenchmarkVerification.Require(
            verify1 == 42, "DSoft scaling", "the depth-1 twin returned " + verify1);
        ScaleCounter.Count = 0;
        var verify2 = _mediator.Send<ScaleVerify2, int>(new ScaleVerify2()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            ScaleCounter.Count, 2, "DSoft scaling", "the depth-2 chain, RUNNING");
        BenchmarkVerification.Require(
            verify2 == 42, "DSoft scaling", "the depth-2 twin returned " + verify2);
        ScaleCounter.Count = 0;
        var verify3 = _mediator.Send<ScaleVerify3, int>(new ScaleVerify3()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            ScaleCounter.Count, 3, "DSoft scaling", "the depth-3 chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "DSoft scaling", "the depth-3 twin returned " + verify3);
        ScaleCounter.Count = 0;
        var verify5 = _mediator.Send<ScaleVerify5, int>(new ScaleVerify5()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            ScaleCounter.Count, 5, "DSoft scaling", "the depth-5 chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "DSoft scaling", "the depth-5 twin returned " + verify5);
        ScaleCounter.Count = 0;
        var verify8 = _mediator.Send<ScaleVerify8, int>(new ScaleVerify8()).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            ScaleCounter.Count, 8, "DSoft scaling", "the depth-8 chain, RUNNING");
        BenchmarkVerification.Require(
            verify8 == 42, "DSoft scaling", "the depth-8 twin returned " + verify8);

        // Warm every path.
        _direct.Handle(M0, default).GetAwaiter().GetResult();
        _mediator.Send<Scale0, int>(M0).GetAwaiter().GetResult();
        _mediator.Send<Scale1, int>(M1).GetAwaiter().GetResult();
        _mediator.Send<Scale2, int>(M2).GetAwaiter().GetResult();
        _mediator.Send<Scale3, int>(M3).GetAwaiter().GetResult();
        _mediator.Send<Scale5, int>(M5).GetAwaiter().GetResult();
        _mediator.Send<Scale8, int>(M8).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall() => await _direct.Handle(M0, default);

    [Benchmark] public async Task<int> Send_0Behaviors() => await _mediator.Send<Scale0, int>(M0);
    [Benchmark] public async Task<int> Send_1Behaviors() => await _mediator.Send<Scale1, int>(M1);
    [Benchmark] public async Task<int> Send_2Behaviors() => await _mediator.Send<Scale2, int>(M2);
    [Benchmark] public async Task<int> Send_3Behaviors() => await _mediator.Send<Scale3, int>(M3);
    [Benchmark] public async Task<int> Send_5Behaviors() => await _mediator.Send<Scale5, int>(M5);
    [Benchmark] public async Task<int> Send_8Behaviors() => await _mediator.Send<Scale8, int>(M8);
}
