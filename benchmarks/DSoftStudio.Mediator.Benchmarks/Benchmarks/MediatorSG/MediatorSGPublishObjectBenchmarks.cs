// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Isolated martinothamar/Mediator-only benchmark: Publish(object) vs typed Publish.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatorSGPublishObjectBenchmarks
{
    private static readonly PingNotificationMediatorSG Notification = new();

    private global::Mediator.IMediator _mediator = null!;
    private global::Mediator.IPublisher _publisher = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        _publisher = _mediator;

        // Warmup both paths
        _mediator.Publish(Notification).GetAwaiter().GetResult();
        _publisher.Publish((object)Notification).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task MediatorSG_Publish_Generic()
        => await _mediator.Publish(Notification);

    [Benchmark]
    public async Task MediatorSG_Publish_Object()
        => await _publisher.Publish((object)Notification);
}
