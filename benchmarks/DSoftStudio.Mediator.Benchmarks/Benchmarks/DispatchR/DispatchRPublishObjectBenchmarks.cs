// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Isolated DispatchR-only benchmark: Publish(object) vs typed Publish.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRPublishObjectBenchmarks
{
    private static readonly PingNotificationDispatchR Notification = new();

    private DispatchR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDispatchR(typeof(PingNotificationDispatchRHandler).Assembly, withPipelines: false, withNotifications: true);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        // Warmup both paths
        _mediator.Publish(Notification, default).GetAwaiter().GetResult();
        _mediator.Publish((object)Notification, default).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task DispatchR_Publish_Generic()
        => await _mediator.Publish(Notification, default);

    [Benchmark]
    public async Task DispatchR_Publish_Object()
        => await _mediator.Publish((object)Notification, default);
}
