// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Isolated DSoft-only benchmark: Publish(object) vs typed Publish.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DSoftPublishObjectBenchmarks
{
    private static readonly PingNotification NotificationMsg = new();

    private IMediator _mediator = null!;
    private IPublisher _publisher = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services).RegisterMediatorHandlers().PrecompileNotifications();
        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        _publisher = _mediator;

        // Warmup both paths
        _mediator.Publish(NotificationMsg).GetAwaiter().GetResult();
        _publisher.Publish((object)NotificationMsg).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task DSoft_Publish_Generic()
        => await _mediator.Publish(NotificationMsg);

    [Benchmark]
    public async Task DSoft_Publish_Object()
        => await _publisher.Publish((object)NotificationMsg);
}
