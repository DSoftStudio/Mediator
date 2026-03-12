// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Isolated DispatchR-only benchmark: Publish notification.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRPublishBenchmarks
{
    private static readonly PingNotificationDispatchR Notification = new();

    private PingNotificationDispatchRHandler _directHandler = null!;
    private DispatchR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingNotificationDispatchRHandler();

        var services = new ServiceCollection();
        services.AddDispatchR(typeof(PingNotificationDispatchRHandler).Assembly, withPipelines: false, withNotifications: true);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        // Warmup
        _directHandler.Handle(Notification, default).GetAwaiter().GetResult();
        _mediator.Publish(Notification, default).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task Direct_Publish()
    {
        await _directHandler.Handle(Notification, default);
    }

    [Benchmark]
    public async Task DispatchR_Publish()
    {
        await _mediator.Publish(Notification, default);
    }
}
