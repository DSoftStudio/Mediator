// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Isolated martinothamar/Mediator-only benchmark: cold-start overhead.
/// Measures building a fresh ServiceProvider, resolving the mediator,
/// and dispatching the first request.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatorSGColdStartBenchmarks
{
    private static readonly PingMediatorSG PingMessage = new();

    private ServiceCollection _cold = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-configure ServiceCollection (mirrors real app startup).
        // Only BuildServiceProvider + resolve + send is measured.
        _cold = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(_cold);
    }

    [Benchmark]
    public async Task<int> MediatorSG_ColdStart()
    {
        using var sp = _cold.BuildServiceProvider();
        var mediator = sp.GetRequiredService<global::Mediator.IMediator>();
        return await mediator.Send(PingMessage);
    }
}
