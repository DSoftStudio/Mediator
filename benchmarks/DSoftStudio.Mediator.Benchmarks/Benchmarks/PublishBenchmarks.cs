// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Measures notification Publish() dispatch overhead.
/// Compares:
/// - direct handler call
/// - DSoftStudio compile-time dispatch
/// - MediatR runtime dispatch
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class PublishBenchmarks
{
    private static readonly PingNotification Notification = new();
    private static readonly PingNotificationMediatR MediatRNotification = new();
    private static readonly PingNotificationDispatchR DispatchRNotification = new();

    private IMediator _mediator = default!;
    private MediatR.IMediator _mediatr = default!;
    private DispatchR.IMediator _dispatchr = default!;
    private PingNotificationHandler _directHandler = default!;

    private IServiceScope _scope = default!;
    private IServiceScope _mediatrScope = default!;
    private IServiceScope _dispatchrScope = default!;

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingNotificationHandler();

        // ── DSoftStudio Mediator ──────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddMediator()
                .RegisterMediatorHandlers()
                .PrecompileNotifications();

            var provider = services.BuildServiceProvider();
            _scope = provider.CreateScope();
            _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        }

        // ── MediatR 14.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            // REQUIRED since MediatR 13+
            services.AddLogging();

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingNotificationMediatRHandler).Assembly));

            var provider = services.BuildServiceProvider();
            _mediatrScope = provider.CreateScope();
            _mediatr = _mediatrScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── DispatchR 2.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddDispatchR(typeof(PingNotificationDispatchRHandler).Assembly, withPipelines: false, withNotifications: true);

            var provider = services.BuildServiceProvider();
            _dispatchrScope = provider.CreateScope();
            _dispatchr = _dispatchrScope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // Warmup all mediators (avoid cold start in benchmarks)
        _mediator.Publish(Notification).GetAwaiter().GetResult();
        _mediatr.Publish(MediatRNotification).GetAwaiter().GetResult();
        _dispatchr.Publish(DispatchRNotification, default).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _mediatrScope?.Dispose();
        _dispatchrScope?.Dispose();
    }

    // ── Baseline ─────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public async Task Direct_Publish()
    {
        await _directHandler.Handle(Notification, default);
    }

    // ── DSoftStudio Mediator ─────────────────────────────────────

    [Benchmark]
    public async Task DSoft_Publish()
    {
        await _mediator.Publish(Notification);
    }

    // ── MediatR ──────────────────────────────────────────────────

    [Benchmark]
    public async Task MediatR_Publish()
    {
        await _mediatr.Publish(MediatRNotification);
    }

    // ── DispatchR ─────────────────────────────────────────────────

    [Benchmark]
    public async Task DispatchR_Publish()
    {
        await _dispatchr.Publish(DispatchRNotification, default);
    }
}