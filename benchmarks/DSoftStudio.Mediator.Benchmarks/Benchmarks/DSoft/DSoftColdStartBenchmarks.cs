// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Isolated DSoft-only benchmark: cold-start overhead.
/// Measures building a fresh ServiceProvider, resolving the mediator,
/// and dispatching the first request.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DSoftColdStartBenchmarks
{
    private static readonly Ping PingMessage = new();

    private ServiceCollection _cold = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-configure ServiceCollection (mirrors real app startup).
        // Only BuildServiceProvider + resolve + send is measured.
        _cold = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(_cold)
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
    }

    [Benchmark]
    public async Task<int> DSoft_ColdStart()
    {
        using var sp = _cold.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        return await mediator.Send<Ping, int>(PingMessage);
    }
}
