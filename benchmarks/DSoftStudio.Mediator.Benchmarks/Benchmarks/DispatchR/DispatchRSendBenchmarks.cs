// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

// ── Why every link here is NON-GENERIC ────────────────────────────
// DispatchR does not chain generic behavior types, even closed at registration. Measured in isolation
// against DispatchR.Mediator 2.3.1: two links registered as GenLink<Req, ValueTask<int>> reported
// registered=2, executed=0, while two non-generic links on the same pair reported registered=2,
// executed=2. GetServices sees them either way, so a verification that counts REGISTRATIONS cannot
// tell the difference -- which is how a whole scaling curve stayed flat and looked like a result.
// Hence one concrete link type per (pair, position), and a verification that counts EXECUTIONS.

public sealed class DRSend3 : global::DispatchR.Abstractions.Send.IRequest<DRSend3, ValueTask<int>>;
public sealed class DRSend3Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRSend3, ValueTask<int>>
{ public ValueTask<int> Handle(DRSend3 r, CancellationToken ct) => new(42); }
public sealed class DRSend5 : global::DispatchR.Abstractions.Send.IRequest<DRSend5, ValueTask<int>>;
public sealed class DRSend5Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRSend5, ValueTask<int>>
{ public ValueTask<int> Handle(DRSend5 r, CancellationToken ct) => new(42); }
public sealed class DRSendVerify3 : global::DispatchR.Abstractions.Send.IRequest<DRSendVerify3, ValueTask<int>>;
public sealed class DRSendVerify3Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify3, ValueTask<int>>
{ public ValueTask<int> Handle(DRSendVerify3 r, CancellationToken ct) => new(42); }
public sealed class DRSendVerify5 : global::DispatchR.Abstractions.Send.IRequest<DRSendVerify5, ValueTask<int>>;
public sealed class DRSendVerify5Handler : global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify5, ValueTask<int>>
{ public ValueTask<int> Handle(DRSendVerify5 r, CancellationToken ct) => new(42); }

internal static class DRSendCounter
{
    public static int Count;
}

public sealed class DRSend3Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend3 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend3Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend3 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend3Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend3 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend5Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend5Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend5Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend5Link4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRSend5Link5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSend5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSend5 request, CancellationToken ct)
    { return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify3Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify3 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify3Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify3 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify3Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify3, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify3, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify3 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify5Link1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify5Link2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify5Link3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify5Link4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}
public sealed class DRVerify5Link5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DRSendVerify5, ValueTask<int>> NextPipeline { get; set; }
    public ValueTask<int> Handle(DRSendVerify5 request, CancellationToken ct)
    { Interlocked.Increment(ref DRSendCounter.Count); return NextPipeline.Handle(request, ct); }
}

/// <summary>
/// Isolated DispatchR-only benchmark: Send with 3 / 5 behaviors.
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
public class DispatchRSendBenchmarks
{
    private static readonly DRSend3 Message3 = new();
    private static readonly DRSend5 Message5 = new();

    private DispatchR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDispatchR(typeof(DRSend3Handler).Assembly, withPipelines: true, withNotifications: false);

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>), typeof(DRSend3Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>), typeof(DRSend3Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>), typeof(DRSend3Link3));

        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>), typeof(DRSend5Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>), typeof(DRSend5Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>), typeof(DRSend5Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>), typeof(DRSend5Link4));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>), typeof(DRSend5Link5));

        // Counting links on their OWN pair, so execution is proved without a second container
        // and without a counter anywhere near a measured row.
        // One twin per measured depth, 3 and 5. A SINGLE five-link pair proved only that the
        // library can chain five somewhere -- not that the 3-behavior row really has three.
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify3, ValueTask<int>>), typeof(DRVerify3Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify3, ValueTask<int>>), typeof(DRVerify3Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify3, ValueTask<int>>), typeof(DRVerify3Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>), typeof(DRVerify5Link1));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>), typeof(DRVerify5Link2));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>), typeof(DRVerify5Link3));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>), typeof(DRVerify5Link4));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSendVerify5, ValueTask<int>>), typeof(DRVerify5Link5));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        // Registered...
        var sp = _scope.ServiceProvider;
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend3, ValueTask<int>>>().Count(), 3,
            "DispatchR Send", "the 3-behavior chain");
        BenchmarkVerification.RequireCount(
            sp.GetServices<global::DispatchR.Abstractions.Send.IPipelineBehavior<DRSend5, ValueTask<int>>>().Count(), 5,
            "DispatchR Send", "the 5-behavior chain");

        // ...and RUNNING, which registration alone does not show for this library.
        DRSendCounter.Count = 0;
        var verify3 = _mediator.Send<DRSendVerify3, ValueTask<int>>(new DRSendVerify3(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRSendCounter.Count, 3, "DispatchR Send", "the 3-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify3 == 42, "DispatchR Send", "the 3-behavior twin returned " + verify3);
        DRSendCounter.Count = 0;
        var verify5 = _mediator.Send<DRSendVerify5, ValueTask<int>>(new DRSendVerify5(), default).GetAwaiter().GetResult();
        BenchmarkVerification.RequireCount(
            DRSendCounter.Count, 5, "DispatchR Send", "the 5-behavior chain, RUNNING");
        BenchmarkVerification.Require(
            verify5 == 42, "DispatchR Send", "the 5-behavior twin returned " + verify5);

        // Warmup.
        _mediator.Send<DRSend3, ValueTask<int>>(Message3, default).GetAwaiter().GetResult();
        _mediator.Send<DRSend5, ValueTask<int>>(Message5, default).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall()
        => await new DRSend3Handler().Handle(Message3, default);

    [Benchmark]
    public async Task<int> DispatchR_Send_3Behaviors()
        => await _mediator.Send<DRSend3, ValueTask<int>>(Message3, default);

    [Benchmark]
    public async Task<int> DispatchR_Send_5Behaviors()
        => await _mediator.Send<DRSend5, ValueTask<int>>(Message5, default);
}
