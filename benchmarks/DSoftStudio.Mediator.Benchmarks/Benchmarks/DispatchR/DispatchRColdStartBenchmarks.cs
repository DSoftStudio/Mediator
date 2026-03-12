// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Isolated DispatchR-only benchmark: cold-start overhead.
/// Measures building a fresh ServiceProvider, resolving the mediator,
/// and dispatching the first request.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRColdStartBenchmarks
{
    private static readonly PingDispatchR PingMessage = new();

    private ServiceCollection _cold = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-configure ServiceCollection (mirrors real app startup).
        // Only BuildServiceProvider + resolve + send is measured.
        _cold = new ServiceCollection();
        _cold.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);
    }

    [Benchmark]
    public async Task<int> DispatchR_ColdStart()
    {
        using var sp = _cold.BuildServiceProvider();
        var mediator = sp.GetRequiredService<DispatchR.IMediator>();
        return await mediator.Send<PingDispatchR, ValueTask<int>>(PingMessage, default);
    }
}
