// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Isolated martinothamar/Mediator-only benchmark: Send(object) vs typed Send.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatorSGSendObjectBenchmarks
{
    private static readonly PingMediatorSG Message = new();

    private global::Mediator.IMediator _mediator = null!;
    private global::Mediator.ISender _sender = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        _sender = _mediator;

        // Warmup both paths
        _mediator.Send(Message).GetAwaiter().GetResult();
        _sender.Send((object)Message).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> MediatorSG_Send_Generic()
        => await _mediator.Send(Message);

    [Benchmark]
    public async Task<object?> MediatorSG_Send_Object()
        => await _sender.Send((object)Message);
}
