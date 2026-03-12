// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Measures Send() dispatch overhead without any pipeline behaviors.
/// Compares:
/// - direct handler call
/// - DSoftStudio compile-time dispatch
/// - MediatR runtime dispatch
/// - DispatchR dispatch
/// - martinothamar/Mediator source-generated dispatch
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class SendNoBehaviorsBenchmarks
{
    private static readonly Ping PingMessage = new();
    private static readonly PingMediatR MediatRMessage = new();
    private static readonly PingDispatchR DispatchRMessage = new();
    private static readonly PingMediatorSG MediatorSGMessage = new();

    private PingHandler _directHandler = null!;
    private IMediator _mediator = null!;
    private MediatR.IMediator _mediatr = null!;
    private DispatchR.IMediator _dispatchr = null!;
    private global::Mediator.IMediator _mediatorsg = null!;

    private IServiceScope _scope = null!;
    private IServiceScope _mediatrScope = null!;
    private IServiceScope _dispatchrScope = null!;
    private IServiceScope _mediatorsgScope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingHandler();

        // ── DSoftStudio Mediator ──────────────────────────────────
        {
            var services = new ServiceCollection();

            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers()
                .PrecompilePipelines();

            var provider = services.BuildServiceProvider();
            _scope = provider.CreateScope();
            _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        }

        // ── MediatR 14.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

            var provider = services.BuildServiceProvider();
            _mediatrScope = provider.CreateScope();
            _mediatr = _mediatrScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── DispatchR 2.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

            var provider = services.BuildServiceProvider();
            _dispatchrScope = provider.CreateScope();
            _dispatchr = _dispatchrScope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // ── martinothamar/Mediator (source-generated) ──────────────────
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);

            var provider = services.BuildServiceProvider();
            _mediatorsgScope = provider.CreateScope();
            _mediatorsg = _mediatorsgScope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        }

        // Warmup all mediators (avoid cold start in benchmarks)
        _directHandler.Handle(PingMessage, default).GetAwaiter().GetResult();
        _mediator.Send<Ping, int>(PingMessage).GetAwaiter().GetResult();
        _mediatr.Send(MediatRMessage).GetAwaiter().GetResult();
        _dispatchr.Send<PingDispatchR, ValueTask<int>>(DispatchRMessage, default).GetAwaiter().GetResult();
        _mediatorsg.Send(MediatorSGMessage).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _mediatrScope?.Dispose();
        _dispatchrScope?.Dispose();
        _mediatorsgScope?.Dispose();
    }

    // ── Baseline ─────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public async Task<int> Direct_Send()
        => await _directHandler.Handle(PingMessage, default);

    // ── DSoftStudio Mediator ─────────────────────────────────────

    [Benchmark]
    public async Task<int> DSoft_Send()
        => await _mediator.Send<Ping, int>(PingMessage);

    // ── MediatR ──────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> MediatR_Send()
        => await _mediatr.Send(MediatRMessage);

    // ── DispatchR ─────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> DispatchR_Send()
        => await _dispatchr.Send<PingDispatchR, ValueTask<int>>(DispatchRMessage, default);

    // ── martinothamar/Mediator (source-generated) ─────────────────

    [Benchmark]
    public async Task<int> MediatorSG_Send()
        => await _mediatorsg.Send(MediatorSGMessage);
}
