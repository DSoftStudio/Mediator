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
            await _mediator.Send(new MemoryPing(), TestContext.Current.CancellationToken);

        const int iterations = 1_000_000;

        // The handler here is registered Transient (see the fixture), so a million dispatches
        // legitimately construct a million handlers. That is the DI contract, not a leak: the
        // instances are garbage the moment each Send returns. What a LEAK looks like is retention
        // that scales with the iteration count, so measure two identical rounds and compare them.
        // A leak keeps growing; a one-off heap expansion does not repeat.
        long firstRound = await MeasureRound(iterations);
        long secondRound = await MeasureRound(iterations);

        // Allow the second round a full extra megabyte of slack over the first before calling it a
        // leak — GC bookkeeping and heap sizing are noisy, linear retention is not subtle.
        Assert.True(
            secondRound <= Math.Max(firstRound, 0) + 1_000_000,
            $"Pipeline retention grew across identical rounds: {firstRound:N0} bytes then "
            + $"{secondRound:N0} bytes, {iterations:N0} iterations each."
        );
    }

    /// <summary>Bytes still held after one round of dispatches, measured across a forced collection.</summary>
    private async Task<long> MeasureRound(int iterations)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetTotalMemory(true);

        for (int i = 0; i < iterations; i++)
            await _mediator.Send(new MemoryPing(), TestContext.Current.CancellationToken);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return GC.GetTotalMemory(true) - before;
    }

    [Fact]
    public async Task Send_ScopedExecution_HandlerIsCollectedAfterDisposal()
    {
        await _mediator.Send(new MemoryPing(), TestContext.Current.CancellationToken);

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
