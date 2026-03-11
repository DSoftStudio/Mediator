// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

public class MemoryPressureTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public MemoryPressureTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();

        // Override the auto-Singleton with Transient so the GC test can verify
        // that non-singleton handlers are collected after scope disposal.
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();

        services.PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_MillionIterations_MemoryGrowthBounded()
    {
        // Warm up the pipeline to stabilize allocations
        for (int i = 0; i < 100; i++)
            await _mediator.Send(new Ping());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        for (int i = 0; i < 1_000_000; i++)
            await _mediator.Send(new Ping());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);

        var growthMb = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        // Memory growth should stay within a reasonable range (< 50 MB)
        // for a million lightweight sends through a cached pipeline.
        growthMb.ShouldBeLessThan(50,
            "mediator should not allocate unbounded memory for repeated sends");
    }

    [Fact]
    public async Task Send_RepeatedCalls_DoNotLeakHandlers()
    {
        // Warm up
        await _mediator.Send(new Ping());

        var weakRef = ExecuteScopedSend();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // The scoped handler should be eligible for GC after scope disposal.
        weakRef.IsAlive.ShouldBeFalse(
            "transient handlers should be collected after scope disposal");
    }

    private WeakReference ExecuteScopedSend()
    {
        using var scope = _provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<Ping, int>>();

        mediator.Send(new Ping()).GetAwaiter().GetResult();

        return new WeakReference(handler);
    }
}
