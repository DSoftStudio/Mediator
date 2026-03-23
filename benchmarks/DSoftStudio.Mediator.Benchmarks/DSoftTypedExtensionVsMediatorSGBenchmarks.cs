// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.VSDiagnostics;

namespace Benchmarks;
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[CPUUsageDiagnoser]
public class DSoftTypedExtensionVsMediatorSGBenchmarks
{
    private static readonly Ping DsoftMessage = new();
    private static readonly PingMediatorSG MsgMessage = new();
    private PingHandler _directHandler = null!;
    // DSoft
    private ISender _dsoftSender = null!;
    private IServiceScope _dsoftScope = null!;
    // martinothamar
    private global::Mediator.IMediator _msgMediator = null!;
    private IServiceScope _msgScope = null!;
    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingHandler();
        // DSoft setup
        {
            var services = new ServiceCollection();
            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services).RegisterMediatorHandlers().PrecompilePipelines();
            var provider = services.BuildServiceProvider();
            _dsoftScope = provider.CreateScope();
            _dsoftSender = _dsoftScope.ServiceProvider.GetRequiredService<IMediator>();
            // Warmup
            _dsoftSender.Send(DsoftMessage).GetAwaiter().GetResult();
        }

        // martinothamar setup
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);
            var provider = services.BuildServiceProvider();
            _msgScope = provider.CreateScope();
            _msgMediator = _msgScope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
            // Warmup
            _msgMediator.Send(MsgMessage).GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _dsoftScope?.Dispose();
        _msgScope?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall() => await _directHandler.Handle(DsoftMessage, default);
    /// <summary>
    /// DSoft typed extension: mediator.Send(new Ping())
    /// Resolves to MediatorTypedExtensions.Send(ISender, Ping, CancellationToken)
    /// — concrete per-type extension, both types preserved at compile time.
    /// </summary>
    [Benchmark]
    public async Task<int> DSoft_TypedExtension() => await _dsoftSender.Send(DsoftMessage);
    /// <summary>
    /// martinothamar: mediator.Send(new PingMediatorSG())
    /// Resolves to IMediator.Send&lt;int&gt;(IRequest&lt;int&gt;)
    /// — 1 generic param, request type erased → type switch dispatch.
    /// </summary>
    [Benchmark]
    public async Task<int> MediatorSG_Send() => await _msgMediator.Send(MsgMessage);
}