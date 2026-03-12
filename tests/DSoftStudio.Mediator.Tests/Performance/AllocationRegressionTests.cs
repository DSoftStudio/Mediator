// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Performance;

/// <summary>
/// Allocation regression tests. These verify that hot paths do NOT allocate
/// beyond the expected baseline. If a future change adds allocations, these
/// tests fail — preventing silent performance regressions from being merged.
///
/// Uses <see cref="GC.GetAllocatedBytesForCurrentThread"/> for precise,
/// deterministic measurement (no timing variance like throughput tests).
/// </summary>
public class AllocationRegressionTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public AllocationRegressionTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();

        // Warmup: ensure all static dispatch tables, caches, and JIT are primed.
        _mediator.Send<PerfPing, int>(new PerfPing()).AsTask().GetAwaiter().GetResult();
        _mediator.Publish(new PerfNotification()).GetAwaiter().GetResult();
        DrainStream(_mediator.CreateStream<PerfStream, int>(new PerfStream()));
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Send ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_HotPath_ZeroAllocation()
    {
        // Baseline: measure allocation of a single Send call.
        // Expected: only the ValueTask<int> boxing (72 bytes on x64).
        const long maxAllowedBytes = 128; // generous ceiling

        var request = new PerfPing();

        // Warmup iteration (ensure no first-call allocations)
        await _mediator.Send<PerfPing, int>(request);

        // Measure
        long before = GC.GetAllocatedBytesForCurrentThread();
        await _mediator.Send<PerfPing, int>(request);
        long after = GC.GetAllocatedBytesForCurrentThread();

        long allocated = after - before;

        allocated.ShouldBeLessThanOrEqualTo(maxAllowedBytes,
            $"Send<PerfPing, int> allocated {allocated} bytes (max {maxAllowedBytes}). " +
            "A future change likely added an allocation on the hot path.");
    }

    [Fact]
    public async Task Send_MultipleCallsStable_NoAllocationGrowth()
    {
        var request = new PerfPing();

        // Warmup
        for (int i = 0; i < 10; i++)
            await _mediator.Send<PerfPing, int>(request);

        // Measure N calls
        const int iterations = 1_000;
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < iterations; i++)
            await _mediator.Send<PerfPing, int>(request);

        long after = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocated = after - before;

        // Per-call allocation should be stable (no growing buffers/lists)
        long perCall = totalAllocated / iterations;
        perCall.ShouldBeLessThanOrEqualTo(128,
            $"Send per-call allocation: {perCall} bytes over {iterations} iterations. " +
            "Possible memory leak or growing buffer.");
    }

    // ── Publish ──────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_HotPath_ZeroAllocation()
    {
        const long maxAllowedBytes = 64; // Publish with sync handler should be zero-alloc

        var notification = new PerfNotification();

        // Warmup
        await _mediator.Publish(notification);

        long before = GC.GetAllocatedBytesForCurrentThread();
        await _mediator.Publish(notification);
        long after = GC.GetAllocatedBytesForCurrentThread();

        long allocated = after - before;

        allocated.ShouldBeLessThanOrEqualTo(maxAllowedBytes,
            $"Publish<PerfNotification> allocated {allocated} bytes (max {maxAllowedBytes}). " +
            "The notification dispatch hot path should be allocation-free.");
    }

    // ── Stream ───────────────────────────────────────────────────────

    [Fact]
    public async Task Stream_HotPath_MinimalAllocation()
    {
        // Streams inherently allocate the IAsyncEnumerator — that's expected.
        // But it should be bounded (no growing allocations per element).
        const long maxAllowedBytes = 512; // async enumerator + state machine

        var request = new PerfStream();

        // Warmup
        DrainStream(_mediator.CreateStream<PerfStream, int>(request));

        long before = GC.GetAllocatedBytesForCurrentThread();
        DrainStream(_mediator.CreateStream<PerfStream, int>(request));
        long after = GC.GetAllocatedBytesForCurrentThread();

        long allocated = after - before;

        allocated.ShouldBeLessThanOrEqualTo(maxAllowedBytes,
            $"CreateStream<PerfStream, int> allocated {allocated} bytes (max {maxAllowedBytes}). " +
            "Stream dispatch should have bounded allocations.");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void DrainStream(IAsyncEnumerable<int> stream)
    {
        var enumerator = stream.GetAsyncEnumerator();
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                // consume
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
