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
/// Measures CreateStream() dispatch and IAsyncEnumerable consumption overhead.
/// Compares:
/// - direct handler call
/// - DSoftStudio compile-time dispatch
/// - MediatR runtime dispatch
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class StreamBenchmarks
{
    private static readonly PingStream StreamMessage = new();
    private static readonly PingStreamMediatR MediatRStreamMessage = new();
    private static readonly PingStreamDispatchR DispatchRStreamMessage = new();
    private static readonly PingStreamMediatorSG MediatorSGStreamMessage = new();

    private IMediator _mediator = null!;
    private MediatR.IMediator _mediatr = null!;
    private DispatchR.IMediator _dispatchr = null!;
    private global::Mediator.IMediator _mediatorsg = null!;
    private PingStreamHandler _directHandler = null!;

    private IServiceScope _scope = null!;
    private IServiceScope _mediatrScope = null!;
    private IServiceScope _dispatchrScope = null!;
    private IServiceScope _mediatorsgScope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingStreamHandler();

        // ── DSoftStudio Mediator ──────────────────────────────────
        {
            var services = new ServiceCollection();

            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers()
                .PrecompileStreams();

            var provider = services.BuildServiceProvider();
            _scope = provider.CreateScope();
            _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        }

        // ── MediatR 14.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingStreamMediatRHandler).Assembly));

            var provider = services.BuildServiceProvider();
            _mediatrScope = provider.CreateScope();
            _mediatr = _mediatrScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── DispatchR 2.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddDispatchR(typeof(PingStreamDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

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

        // Warmup all mediators
        Consume(_mediator.CreateStream<PingStream, int>(StreamMessage)).GetAwaiter().GetResult();
        Consume(_mediatr.CreateStream(MediatRStreamMessage)).GetAwaiter().GetResult();
        Consume(_dispatchr.CreateStream<PingStreamDispatchR, int>(DispatchRStreamMessage, default)).GetAwaiter().GetResult();
        Consume(_mediatorsg.CreateStream(MediatorSGStreamMessage)).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _mediatrScope?.Dispose();
        _dispatchrScope?.Dispose();
        _mediatorsgScope?.Dispose();
    }

    private static async Task<int> Consume(IAsyncEnumerable<int> stream)
    {
        int result = 0;

        await foreach (var item in stream)
            result = item;

        return result;
    }

    // ── Baseline ─────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public async Task<int> Direct_Stream()
        => await Consume(_directHandler.Handle(StreamMessage, default));

    // ── DSoftStudio Mediator ─────────────────────────────────────

    [Benchmark]
    public async Task<int> DSoft_Stream()
        => await Consume(_mediator.CreateStream<PingStream, int>(StreamMessage));

    // ── MediatR ──────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> MediatR_Stream()
        => await Consume(_mediatr.CreateStream(MediatRStreamMessage));

    // ── DispatchR ─────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> DispatchR_Stream()
        => await Consume(_dispatchr.CreateStream<PingStreamDispatchR, int>(DispatchRStreamMessage, default));

    // ── martinothamar/Mediator (source-generated) ─────────────────

    [Benchmark]
    public async Task<int> MediatorSG_Stream()
        => await Consume(_mediatorsg.CreateStream(MediatorSGStreamMessage));
}