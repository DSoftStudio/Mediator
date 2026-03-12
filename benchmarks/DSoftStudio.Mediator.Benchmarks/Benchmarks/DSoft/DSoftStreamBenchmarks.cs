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
[SimpleJob]
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

        // Warmup
        Consume(_directHandler.Handle(StreamMessage, default)).GetAwaiter().GetResult();
        Consume(_mediator.CreateStream<PingStream, int>(StreamMessage)).GetAwaiter().GetResult();
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
