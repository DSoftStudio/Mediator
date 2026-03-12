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
[SimpleJob]
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

        // Warmup
        Consume(_directHandler.Handle(StreamMessage, default)).GetAwaiter().GetResult();
        Consume(_mediator.CreateStream<PingStreamDispatchR, int>(StreamMessage, default)).GetAwaiter().GetResult();
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
