// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

/// <summary>
/// Isolated MediatR-only benchmark: Publish(object) vs typed Publish.
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatRPublishObjectBenchmarks
{
    private static readonly PingNotificationMediatR Notification = new();

    private MediatR.IMediator _mediator = null!;
    private MediatR.IPublisher _publisher = null!;
    private IServiceScope _scope = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingNotificationMediatRHandler).Assembly));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        _publisher = _mediator;

        // Warmup both paths
        _mediator.Publish(Notification).GetAwaiter().GetResult();
        _publisher.Publish((object)Notification).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task MediatR_Publish_Generic()
        => await _mediator.Publish(Notification);

    [Benchmark]
    public async Task MediatR_Publish_Object()
        => await _publisher.Publish((object)Notification);
}
