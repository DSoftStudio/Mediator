// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
namespace DSoftStudio.Mediator.Tests.Security;

// ── Request / handler unique to GC leak tests ─────────────────────

public sealed record MemoryPing : IRequest<int>;

public sealed class MemoryPingHandler : IRequestHandler<MemoryPing, int>
{
    public ValueTask<int> Handle(MemoryPing request, CancellationToken cancellationToken)
        => new(1);
}

// ── Tests ─────────────────────────────────────────────────────────

public class PipelineGcLeakTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public PipelineGcLeakTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();

        // Override the auto-Singleton with Transient so the GC test can verify
        // that non-singleton handlers are collected after scope disposal.
        services.AddTransient<IRequestHandler<MemoryPing, int>, MemoryPingHandler>();

        services.PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_MillionRequests_NoPipelineMemoryLeak()
    {
        // Warm pipeline
        for (int i = 0; i < 100; i++)
            await _mediator.Send(new MemoryPing());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memoryBefore = GC.GetTotalMemory(true);

        const int iterations = 1_000_000;

        for (int i = 0; i < iterations; i++)
            await _mediator.Send(new MemoryPing());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memoryAfter = GC.GetTotalMemory(true);

        long difference = memoryAfter - memoryBefore;

        Assert.True(
            difference < 2_000_000,
            $"Pipeline leaked {difference:N0} bytes after {iterations:N0} iterations."
        );
    }

    [Fact]
    public async Task Send_ScopedExecution_HandlerIsCollectedAfterDisposal()
    {
        await _mediator.Send(new MemoryPing());

        var weakRef = ResolveSendAndTrackHandler();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(
            weakRef.IsAlive,
            "transient handler should be eligible for GC after scope disposal"
        );
    }

    private WeakReference ResolveSendAndTrackHandler()
    {
        using var scope = _provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<MemoryPing, int>>();

        mediator.Send(new MemoryPing()).GetAwaiter().GetResult();

        return new WeakReference(handler);
    }
}
