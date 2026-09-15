// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

// ── Requests: one type per behavior count ─────────────────────────
// Mirrors DSoftBehaviorScalingBenchmarks exactly: the same depths, the same pass-through links, the
// same warmup, so the four curves are comparable.
//
// Every link here is NON-GENERIC, and that is not a style choice. DispatchR does not chain generic
// behavior types even when they are closed at registration: measured in isolation against
// DispatchR.Mediator 2.3.1, two links registered as GenLink<Req, ValueTask<int>> reported
// registered=2, executed=0, while two non-generic links on the same pair reported registered=2,
// executed=2. This suite used generic links and verified only REGISTRATIONS, so every measured chain
// was empty and the curve was flat by construction — a fiction that looked like a result. The
// verification below now counts EXECUTIONS.
public sealed class DRScale0 : global::DispatchR.Abstractions.Send.IRequest<DRScale0, ValueTask<int>>;
public sealed class DRScale0Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScale0, ValueTask<int>>
{ public ValueTask<int> Handle(DRScale0 r, CancellationToken ct) => new(42); }
public sealed class DRScale1 : global::DispatchR.Abstractions.Send.IRequest<DRScale1, ValueTask<int>>;
public sealed class DRScale1Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScale1, ValueTask<int>>
{ public ValueTask<int> Handle(DRScale1 r, CancellationToken ct) => new(42); }
public sealed class DRScale2 : global::DispatchR.Abstractions.Send.IRequest<DRScale2, ValueTask<int>>;
public sealed class DRScale2Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScale2, ValueTask<int>>
{ public ValueTask<int> Handle(DRScale2 r, CancellationToken ct) => new(42); }
public sealed class DRScale3 : global::DispatchR.Abstractions.Send.IRequest<DRScale3, ValueTask<int>>;
public sealed class DRScale3Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScale3, ValueTask<int>>
{ public ValueTask<int> Handle(DRScale3 r, CancellationToken ct) => new(42); }
public sealed class DRScale5 : global::DispatchR.Abstractions.Send.IRequest<DRScale5, ValueTask<int>>;
public sealed class DRScale5Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScale5, ValueTask<int>>
{ public ValueTask<int> Handle(DRScale5 r, CancellationToken ct) => new(42); }
public sealed class DRScale8 : global::DispatchR.Abstractions.Send.IRequest<DRScale8, ValueTask<int>>;
public sealed class DRScale8Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>>
{ public ValueTask<int> Handle(DRScale8 r, CancellationToken ct) => new(42); }
public sealed class DRScaleVerify0 : global::DispatchR.Abstractions.Send.IRequest<DRScaleVerify0, ValueTask<int>>;
public sealed class DRScaleVerify0Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify0, ValueTask<int>>
{ public ValueTask<int> Handle(DRScaleVerify0 r, CancellationToken ct) => new(42); }
public sealed class DRScaleVerify1 : global::DispatchR.Abstractions.Send.IRequest<DRScaleVerify1, ValueTask<int>>;
public sealed class DRScaleVerify1Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify1, ValueTask<int>>
{ public ValueTask<int> Handle(DRScaleVerify1 r, CancellationToken ct) => new(42); }
public sealed class DRScaleVerify2 : global::DispatchR.Abstractions.Send.IRequest<DRScaleVerify2, ValueTask<int>>;
public sealed class DRScaleVerify2Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify2, ValueTask<int>>
{ public ValueTask<int> Handle(DRScaleVerify2 r, CancellationToken ct) => new(42); }
public sealed class DRScaleVerify3 : global::DispatchR.Abstractions.Send.IRequest<DRScaleVerify3, ValueTask<int>>;
public sealed class DRScaleVerify3Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify3, ValueTask<int>>
{ public ValueTask<int> Handle(DRScaleVerify3 r, CancellationToken ct) => new(42); }
public sealed class DRScaleVerify5 : global::DispatchR.Abstractions.Send.IRequest<DRScaleVerify5, ValueTask<int>>;
public sealed class DRScaleVerify5Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify5, ValueTask<int>>
{ public ValueTask<int> Handle(DRScaleVerify5 r, CancellationToken ct) => new(42); }
public sealed class DRScaleVerify8 : global::DispatchR.Abstractions.Send.IRequest<DRScaleVerify8, ValueTask<int>>;
public sealed class DRScaleVerify8Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>>
{ public ValueTask<int> Handle(DRScaleVerify8 r, CancellationToken ct) => new(42); }

internal static class DRScaleCounter
{
    public static int Count;
}

public sealed class DRScale1Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale1, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale1, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale1 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale2Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale2, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale2, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale2 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale2Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale2, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale2, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale2 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale3Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale3 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale3Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale3 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale3Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale3 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale5Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale5Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale5Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale5Link4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale5Link5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link6 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link7 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRScale8Link8 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScale8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScale8 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
// One concrete counting link per (twin, position): this library does not chain generic types,
// and a twin must mirror the depth it stands for exactly.
public sealed class DRScaleVerify1Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify1, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify1, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify1 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify2Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify2, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify2, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify2 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify2Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify2, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify2, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify2 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify3Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify3 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify3Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify3 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify3Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify3 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify5Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify5Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify5Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify5Link4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify5Link5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link6 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link7 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRScaleVerify8Link8 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRScaleVerify8, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRScaleVerify8 request, CancellationToken ct)
    { Interlocked.Increment(ref DRScaleCounter.Count); return NextPipeline.Handle(request, ct); }
}

/// <summary>
/// How DispatchR's dispatch cost scales with the number of pipeline behaviors.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 12, iterationCount: 30)]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
public class DispatchRBehaviorScalingBenchmarks
{
    private static readonly DRScale0 M0 = new();
    private static readonly DRScale1 M1 = new();
    private static readonly DRScale2 M2 = new();
    private static readonly DRScale3 M3 = new();
    private static readonly DRScale5 M5 = new();
    private static readonly DRScale8 M8 = new();

    private IServiceScope _scope = null!;
    private DispatchR.IMediator _mediator = null!;
    private DRScale0Handler _direct = null!;

    [GlobalSetup]
    public void Setup()
    {
        _direct = new DRScale0Handler();

        var services = new ServiceCollection();
        services.AddDispatchR(typeof(DRScale0Handler).Assembly, withPipelines: true, withNotifications: false);

        // DRScale0 deliberately gets no behaviors at all.

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale1, ValueTask<int>>), typeof(DRScale1Link1));

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale2, ValueTask<int>>), typeof(DRScale2Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale2, ValueTask<int>>), typeof(DRScale2Link2));

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>), typeof(DRScale3Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>), typeof(DRScale3Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>), typeof(DRScale3Link3));

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>), typeof(DRScale5Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>), typeof(DRScale5Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>), typeof(DRScale5Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>), typeof(DRScale5Link4));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>), typeof(DRScale5Link5));

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link4));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link5));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link6));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link7));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>), typeof(DRScale8Link8));

        // Counting links on their OWN pair: registration alone does not show that this library
        // actually chains what was registered.
        // One twin per measured depth. Verifying a SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the depth-3 row has three and the depth-8
        // row has eight. That is the gap this suite's flat curve walked through the first time.
        // The depth-0 twin must run ZERO: it catches a registration landing on the wrong pair.
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify1, ValueTask<int>>), typeof(DRScaleVerify1Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify2, ValueTask<int>>), typeof(DRScaleVerify2Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify2, ValueTask<int>>), typeof(DRScaleVerify2Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify3, ValueTask<int>>), typeof(DRScaleVerify3Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify3, ValueTask<int>>), typeof(DRScaleVerify3Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify3, ValueTask<int>>), typeof(DRScaleVerify3Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>), typeof(DRScaleVerify5Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>), typeof(DRScaleVerify5Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>), typeof(DRScaleVerify5Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>), typeof(DRScaleVerify5Link4));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify5, ValueTask<int>>), typeof(DRScaleVerify5Link5));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link4));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link5));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link6));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link7));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScaleVerify8, ValueTask<int>>), typeof(DRScaleVerify8Link8));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        var sp = _scope.ServiceProvider;
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale0, ValueTask<int>>>().Count(), 0,
            "DispatchR scaling", "the depth-0 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale1, ValueTask<int>>>().Count(), 1,
            "DispatchR scaling", "the depth-1 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale2, ValueTask<int>>>().Count(), 2,
            "DispatchR scaling", "the depth-2 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale3, ValueTask<int>>>().Count(), 3,
            "DispatchR scaling", "the depth-3 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale5, ValueTask<int>>>().Count(), 5,
            "DispatchR scaling", "the depth-5 behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRScale8, ValueTask<int>>>().Count(), 8,
            "DispatchR scaling", "the depth-8 behavior chain");

        // And they RUN, not merely resolve.
        DRScaleCounter.Count = 0;
        var verify0 = _mediator.Send<DRScaleVerify0, ValueTask<int>>(new DRScaleVerify0(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRScaleCounter.Count, 0, "DispatchR scaling", "the depth-0 chain, RUNNING");
        BenchmarkVerification.Require(
            verify0 == 42, "DispatchR scaling", "the depth-0 twin returned " + verify0);
        DRScaleCounter.Count = 0;
        var verify1 = _mediator.Send<DRScaleVerify1, ValueTask<int>>(new DRScaleVerify1(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRScaleCounter.Count, 1, "DispatchR scaling", "the depth-1 chain, RUNNING");
        BenchmarkVerification.Require(
            verify1 == 42, "DispatchR scaling", "the depth-1 twin returned " + verify1);
        DRScaleCounter.Count = 0;
        var verify2 = _mediator.Send<DRScaleVerify2, ValueTask<int>>(new DRScaleVerify2(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRScaleCounter.Count, 2, "DispatchR scaling", "the depth-2 chain, RUNNING");
        BenchmarkVerification.Require(
            verify2 == 42, "DispatchR scaling", "the depth-2 twin returned " + verify2);
        DRScaleCounter.Count = 0;
        var verify3 = _mediator.Send<DRScaleVerify3, ValueTask<int>>(new DRScaleVerify3(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRScaleCounter.Count, 3, "DispatchR scaling", "the depth-3 chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "DispatchR scaling", "the depth-3 twin returned " + verify3);
        DRScaleCounter.Count = 0;
        var verify5 = _mediator.Send<DRScaleVerify5, ValueTask<int>>(new DRScaleVerify5(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRScaleCounter.Count, 5, "DispatchR scaling", "the depth-5 chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "DispatchR scaling", "the depth-5 twin returned " + verify5);
        DRScaleCounter.Count = 0;
        var verify8 = _mediator.Send<DRScaleVerify8, ValueTask<int>>(new DRScaleVerify8(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRScaleCounter.Count, 8, "DispatchR scaling", "the depth-8 chain, RUNNING");
        BenchmarkVerification.Require(
            verify8 == 42, "DispatchR scaling", "the depth-8 twin returned " + verify8);

        // Warm every path.
        _direct.Handle(M0, default).GetAwaiter().GetResult();
        _mediator.Send<DRScale0, ValueTask<int>>(M0, default).GetAwaiter().GetResult();
        _mediator.Send<DRScale1, ValueTask<int>>(M1, default).GetAwaiter().GetResult();
        _mediator.Send<DRScale2, ValueTask<int>>(M2, default).GetAwaiter().GetResult();
        _mediator.Send<DRScale3, ValueTask<int>>(M3, default).GetAwaiter().GetResult();
        _mediator.Send<DRScale5, ValueTask<int>>(M5, default).GetAwaiter().GetResult();
        _mediator.Send<DRScale8, ValueTask<int>>(M8, default).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall() => await _direct.Handle(M0, default);

    [Benchmark] public async Task<int> Send_0Behaviors() => await _mediator.Send<DRScale0, ValueTask<int>>(M0, default);
    [Benchmark] public async Task<int> Send_1Behaviors() => await _mediator.Send<DRScale1, ValueTask<int>>(M1, default);
    [Benchmark] public async Task<int> Send_2Behaviors() => await _mediator.Send<DRScale2, ValueTask<int>>(M2, default);
    [Benchmark] public async Task<int> Send_3Behaviors() => await _mediator.Send<DRScale3, ValueTask<int>>(M3, default);
    [Benchmark] public async Task<int> Send_5Behaviors() => await _mediator.Send<DRScale5, ValueTask<int>>(M5, default);
    [Benchmark] public async Task<int> Send_8Behaviors() => await _mediator.Send<DRScale8, ValueTask<int>>(M8, default);
}
