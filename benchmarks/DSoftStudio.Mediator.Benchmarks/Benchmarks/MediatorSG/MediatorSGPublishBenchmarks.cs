// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Isolated martinothamar/Mediator-only benchmark: Publish notification.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatorSGPublishBenchmarks
{
    private static readonly PingNotificationMediatorSG Notification = new();

    private PingNotificationMediatorSGHandler _directHandler = null!;
    private global::Mediator.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingNotificationMediatorSGHandler();

        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();

        // Warmup
        _directHandler.Handle(Notification, default).GetAwaiter().GetResult();
        _mediator.Publish(Notification).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task Direct_Publish()
    {
        await _directHandler.Handle(Notification, default);
    }

    [Benchmark]
    public async Task MediatorSG_Publish()
    {
        await _mediator.Publish(Notification);
    }
}
