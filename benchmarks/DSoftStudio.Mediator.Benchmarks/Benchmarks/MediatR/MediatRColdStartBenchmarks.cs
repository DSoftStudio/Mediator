// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

/// <summary>
/// Isolated MediatR-only benchmark: cold-start overhead.
/// Measures building a fresh ServiceProvider, resolving the mediator,
/// and dispatching the first request.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatRColdStartBenchmarks
{
    private static readonly PingMediatR PingMessage = new();

    private ServiceCollection _cold = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-configure ServiceCollection (mirrors real app startup).
        // Only BuildServiceProvider + resolve + send is measured.
        _cold = new ServiceCollection();
        _cold.AddLogging();
        _cold.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));
    }

    [Benchmark]
    public async Task<int> MediatR_ColdStart()
    {
        using var sp = _cold.BuildServiceProvider();
        var mediator = sp.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(PingMessage);
    }
}
