// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Isolated DSoft-only benchmark: Send(object) vs typed Send.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DSoftSendObjectBenchmarks
{
    private static readonly Ping PingMessage = new();

    private IMediator _mediator = null!;
    private ISender _sender = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services).RegisterMediatorHandlers().PrecompilePipelines();
        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        _sender = _mediator;

        // Warmup both paths
        _mediator.Send<Ping, int>(PingMessage).GetAwaiter().GetResult();
        _sender.Send((object)PingMessage).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> DSoft_Send_Generic()
        => await _mediator.Send<Ping, int>(PingMessage);

    [Benchmark]
    public async Task<object?> DSoft_Send_Object()
        => await _sender.Send((object)PingMessage);
}
