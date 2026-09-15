// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Isolated DSoft-only benchmark: CreateStream + IAsyncEnumerable consumption.
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
public class DSoftStreamBenchmarks
{
    private static readonly PingStream StreamMessage = new();

    private PingStreamHandler _directHandler = null!;
    private IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingStreamHandler();

        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers()
            .PrecompileStreams();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();

        // Warm both paths AND check they produced the item. Consume() returns the LAST value,
        // so a stream that yields nothing returns 0 silently -- and an empty enumeration is very
        // fast, which is the shape of a fiction that looks like a result.
        var directValue = Consume(_directHandler.Handle(StreamMessage, default)).GetAwaiter().GetResult();
        var mediatorValue = Consume(_mediator.CreateStream<PingStream, int>(StreamMessage)).GetAwaiter().GetResult();
        BenchmarkVerification.Require(
            directValue == 42, "DSoft stream", "the direct handler yielded " + directValue);
        BenchmarkVerification.Require(
            mediatorValue == 42, "DSoft stream", "the mediator stream yielded " + mediatorValue);
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
    public async Task<int> DSoft_Stream()
        => await Consume(_mediator.CreateStream<PingStream, int>(StreamMessage));
}
