// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Isolated DispatchR-only benchmark: Send without behaviors.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRSendNoBehaviorsBenchmarks
{
    private static readonly PingDispatchR Message = new();

    private DispatchR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        // Warmup
        _mediator.Send<PingDispatchR, ValueTask<int>>(Message, default).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark]
    public async Task<int> DispatchR_Send()
        => await _mediator.Send<PingDispatchR, ValueTask<int>>(Message, default);
}
