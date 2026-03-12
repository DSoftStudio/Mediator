// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Measures cold-start overhead: building a fresh ServiceProvider,
/// resolving the mediator, and dispatching the first request.
/// Isolated in its own class so BenchmarkDotNet runs it in a
/// separate process — no warm static dispatch tables from other benchmarks.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class ColdStartBenchmarks
{
    private static readonly Ping PingMessage = new();
    private static readonly PingMediatR PingMediatRMessage = new();
    private static readonly PingDispatchR PingDispatchRMessage = new();
    private static readonly PingMediatorSG PingMediatorSGMessage = new();

    private ServiceCollection _coldDSoft = null!;
    private ServiceCollection _coldMediatR = null!;
    private ServiceCollection _coldDispatchR = null!;
    private ServiceCollection _coldMediatorSG = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-configure ServiceCollections (mirrors real app startup).
        // Only BuildServiceProvider + resolve + send is measured.

        _coldDSoft = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(_coldDSoft)
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        _coldMediatR = new ServiceCollection();
        _coldMediatR.AddLogging();
        _coldMediatR.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

        _coldDispatchR = new ServiceCollection();
        _coldDispatchR.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

        _coldMediatorSG = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(_coldMediatorSG);
    }

    // ── Benchmarks ───────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public async Task<int> DSoft_ColdStart()
    {
        using var sp = _coldDSoft.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        return await mediator.Send<Ping, int>(PingMessage);
    }

    [Benchmark]
    public async Task<int> MediatR_ColdStart()
    {
        using var sp = _coldMediatR.BuildServiceProvider();
        var mediator = sp.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(PingMediatRMessage);
    }

    [Benchmark]
    public async Task<int> DispatchR_ColdStart()
    {
        using var sp = _coldDispatchR.BuildServiceProvider();
        var mediator = sp.GetRequiredService<DispatchR.IMediator>();
        return await mediator.Send<PingDispatchR, ValueTask<int>>(PingDispatchRMessage, default);
    }

    [Benchmark]
    public async Task<int> MediatorSG_ColdStart()
    {
        using var sp = _coldMediatorSG.BuildServiceProvider();
        var mediator = sp.GetRequiredService<global::Mediator.IMediator>();
        return await mediator.Send(PingMediatorSGMessage);
    }
}
