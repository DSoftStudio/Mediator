// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Isolated DispatchR-only benchmark: CreateStream + IAsyncEnumerable consumption.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
// Same job as SendBenchmarks and BehaviorScalingBenchmarks. The default was already caught
// measuring mid-tiering elsewhere -- StdDev 2.86 falling to 0.02 with twelve warmup
// iterations -- and a one-item stream is mostly enumerator setup, which is exactly the part
// that takes longest to settle.
[SimpleJob(warmupCount: 12, iterationCount: 30)]
[MedianColumn]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRStreamBenchmarks
{
    private static readonly PingStreamDispatchR StreamMessage = new();

    private PingStreamDispatchRHandler _directHandler = null!;
    private DispatchR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingStreamDispatchRHandler();

        var services = new ServiceCollection();
        services.AddDispatchR(typeof(PingStreamDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        // Warm both paths AND check they produced the item. Consume() returns the LAST value,
        // so a stream that yields nothing returns 0 silently -- and an empty enumeration is very
        // fast, which is the shape of a fiction that looks like a result.
        var directValue = Consume(_directHandler.Handle(StreamMessage, default)).GetAwaiter().GetResult();
        var mediatorValue = Consume(_mediator.CreateStream<PingStreamDispatchR, int>(StreamMessage, default)).GetAwaiter().GetResult();
        BenchmarkVerification.Require(
            directValue == 42, "DispatchR stream", "the direct handler yielded " + directValue);
        BenchmarkVerification.Require(
            mediatorValue == 42, "DispatchR stream", "the mediator stream yielded " + mediatorValue);
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    private static async Task<int> Consume(IAsyncEnumerable<int> stream)
    {
        int result = 0;

        await foreach (var item in stream)
            result = item;

        return result;
    }

    [Benchmark(Baseline = true)]
    public async Task<int> Direct_Stream()
        => await Consume(_directHandler.Handle(StreamMessage, default));

    [Benchmark]
    public async Task<int> DispatchR_Stream()
        => await Consume(_mediator.CreateStream<PingStreamDispatchR, int>(StreamMessage, default));
}
