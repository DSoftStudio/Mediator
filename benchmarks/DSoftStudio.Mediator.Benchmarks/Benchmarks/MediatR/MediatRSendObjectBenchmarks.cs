// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

/// <summary>
/// Isolated MediatR-only benchmark: Send(object) vs typed Send.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatRSendObjectBenchmarks
{
    private static readonly PingMediatR Message = new();

    private MediatR.IMediator _mediator = null!;
    private MediatR.ISender _sender = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        _sender = _mediator;

        // Warmup both paths
        _mediator.Send(Message).GetAwaiter().GetResult();
        _sender.Send((object)Message).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<int> MediatR_Send_Generic()
        => await _mediator.Send(Message);

    [Benchmark]
    public async Task<object?> MediatR_Send_Object()
        => await _sender.Send((object)Message);
}
