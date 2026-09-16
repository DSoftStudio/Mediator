// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
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
// The table has to answer "what does this library add" itself. Three rows separate the DI
// floor from registration from the first dispatch, but a reader who does not subtract walks
// away with a total that is mostly .NET and the container.
[Config(typeof(AddedOverFloorConfig))]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DSoftColdStartBenchmarks
{
    private static readonly Ping PingMessage = new();

    // ── Registration is measured, not set up ──────────────────────────
    // It used to live in [GlobalSetup], which excluded it from every row AND pre-JITted the
    // machinery the measured row then reused — so the reported figure was neither the library's cost
    // nor an honest floor. Registration is library work: an application cannot serve a request until
    // it has run, so time-to-first-request has to contain it.
    //
    // Three rows make the whole path visible, each a superset of the one above:
    //   DiFloor          the floor: a container holding one trivial service, resolved
    //   Registered       + AddMediator / RegisterMediatorHandlers / PrecompilePipelines, and resolve
    //   FirstRequest     + the first dispatch
    // Registered - DiFloor is what standing the library up costs; FirstRequest - Registered is the
    // first dispatch; FirstRequest - DiFloor is the whole of what the library adds to
    // time-to-first-request, which is the number an application actually feels.
    //
    // The floor RESOLVES, it does not merely build. Measured on a container holding one trivial
    // service: building took 6.80 ms and the first resolve 5.44 ms. A baseline that only built
    // left those 5.44 ms to be charged to the library, which is how this one's startup first read
    // as 28.5 ms when it is closer to 9.

    [Benchmark(Baseline = true)]
    public int DSoft_Startup_DiFloor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStartupFloor, StartupFloor>();

        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IStartupFloor>().Value;
    }

    [Benchmark]
    public int DSoft_Startup_Registered()
    {
        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IMediator>() is null ? 0 : 1;
    }

    [Benchmark]
    public async Task<int> DSoft_Startup_FirstRequest()
    {
        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        return await mediator.Send<Ping, int>(PingMessage);
    }
}
