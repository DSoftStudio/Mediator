// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
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
// ── One process per sample, or this measures nothing cold ─────────
// A new ServiceProvider per invocation is NOT a cold start: the dispatch caches are process-wide
// statics, so the first invocation arms them and every later one measures a warm dispatch behind a
// fresh provider. The give-away was the result itself — 2.6 us with a StdDev of 0.02, far too
// steady for a path that includes JITting the dispatch machinery.
//
// ColdStart skips the pilot; launchCount spawns a FRESH PROCESS per sample and
// invocationCount 1 keeps each process to the single measured dispatch, so the JIT and the static
// arming land inside the measurement instead of before it. Warmup is 0 for the same reason — it is
// the one suite here where warming up destroys what is being measured.
[SimpleJob(RunStrategy.ColdStart, launchCount: 40, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
[MedianColumn]
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

    // Everything the measured row does EXCEPT the dispatch. Cold start is dominated by process and
    // JIT costs that have nothing to do with the mediator, so without this the comparison between
    // libraries would be a comparison of .NET startup. The gap between the two rows is the part
    // that is actually about the library.
    [Benchmark(Baseline = true)]
    public int MediatR_Startup_ContainerOnly()
    {
        using var sp = _cold.BuildServiceProvider();
        return sp.GetRequiredService<MediatR.IMediator>() is null ? 0 : 1;
    }

    [Benchmark]
    public async Task<int> MediatR_Startup_WithFirstDispatch()
    {
        using var sp = _cold.BuildServiceProvider();
        var mediator = sp.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(PingMessage);
    }
}
