// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Performance;

/// <summary>
/// Throughput regression tests. These verify that the mediator maintains
/// acceptable throughput on hot paths. Thresholds are generous (10x headroom)
/// to avoid flaky failures on CI while still catching catastrophic regressions
/// (e.g., accidental O(n²) or lock contention introduced in a PR).
///
/// These tests do NOT replace BenchmarkDotNet for precise measurement —
/// they are guardrails against order-of-magnitude regressions.
/// </summary>
public class ThroughputRegressionTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    // ── Thresholds ──────────────────────────────────────────────────
    // Based on benchmarks: Send ~8ns, Publish ~8ns on i7-12700F.
    // GitHub Actions shared runners (2 vCPU) can be 10-50x slower due to:
    //   - CPU steal time (noisy neighbors)
    //   - GC pauses (Gen2 = 10-50ms, amortized over iterations)
    //   - Thread pool starvation on 2-vCPU VMs
    // Thresholds use ~1000x headroom: catches O(n²)/lock regressions, not micro-optimizations.

    /// <summary>Max microseconds per Send call (10K iterations average).</summary>
    private const double MaxSendMicroseconds = 50.0;

    /// <summary>Max microseconds per Publish call (10K iterations average).</summary>
    private const double MaxPublishMicroseconds = 50.0;

    /// <summary>Max microseconds per Stream drain (1K iterations average).</summary>
    private const double MaxStreamMicroseconds = 100.0;

    public ThroughputRegressionTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();

        // Warmup all paths
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
    public async Task Send_10K_Iterations_WithinThroughputThreshold()
    {
        const int iterations = 10_000;
        var request = new PerfPing();

        // Warmup
        for (int i = 0; i < 100; i++)
            await _mediator.Send<PerfPing, int>(request);

        ForceGC();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
            await _mediator.Send<PerfPing, int>(request);

        sw.Stop();

        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;

        avgMicroseconds.ShouldBeLessThan(MaxSendMicroseconds,
            $"Send avg: {avgMicroseconds:F3}μs/call over {iterations} iterations " +
            $"(threshold: {MaxSendMicroseconds}μs). Possible regression.");
    }

    [Fact]
    public async Task Send_ConcurrentThroughput_WithinThreshold()
    {
        const int tasksCount = 10;
        const int iterationsPerTask = 1_000;
        var request = new PerfPing();

        // Warmup
        for (int i = 0; i < 100; i++)
            await _mediator.Send<PerfPing, int>(request);

        ForceGC();
        var sw = Stopwatch.StartNew();

        var tasks = new Task[tasksCount];
        for (int t = 0; t < tasksCount; t++)
        {
            tasks[t] = Task.Run(async () =>
            {
                for (int i = 0; i < iterationsPerTask; i++)
                    await _mediator.Send<PerfPing, int>(request);
            });
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        int totalIterations = tasksCount * iterationsPerTask;
        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / totalIterations;

        avgMicroseconds.ShouldBeLessThan(MaxSendMicroseconds,
            $"Concurrent Send avg: {avgMicroseconds:F3}μs/call " +
            $"({tasksCount} tasks × {iterationsPerTask} iterations, threshold: {MaxSendMicroseconds}μs). " +
            "Possible lock contention regression.");
    }

    // ── Publish ──────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_10K_Iterations_WithinThroughputThreshold()
    {
        const int iterations = 10_000;
        var notification = new PerfNotification();

        // Warmup
        for (int i = 0; i < 100; i++)
            await _mediator.Publish(notification);

        ForceGC();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
            await _mediator.Publish(notification);

        sw.Stop();

        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;

        avgMicroseconds.ShouldBeLessThan(MaxPublishMicroseconds,
            $"Publish avg: {avgMicroseconds:F3}μs/call over {iterations} iterations " +
            $"(threshold: {MaxPublishMicroseconds}μs). Possible regression.");
    }

    // ── Stream ───────────────────────────────────────────────────────

    [Fact]
    public void Stream_1K_Iterations_WithinThroughputThreshold()
    {
        const int iterations = 1_000;
        var request = new PerfStream();

        // Warmup
        for (int i = 0; i < 10; i++)
            DrainStream(_mediator.CreateStream<PerfStream, int>(request));

        ForceGC();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
            DrainStream(_mediator.CreateStream<PerfStream, int>(request));

        sw.Stop();

        double avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;

        avgMicroseconds.ShouldBeLessThan(MaxStreamMicroseconds,
            $"Stream avg: {avgMicroseconds:F3}μs/call over {iterations} iterations " +
            $"(threshold: {MaxStreamMicroseconds}μs). Possible regression.");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void ForceGC()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    private static void DrainStream(IAsyncEnumerable<int> stream)
    {
        var enumerator = stream.GetAsyncEnumerator();
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult()) { }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
