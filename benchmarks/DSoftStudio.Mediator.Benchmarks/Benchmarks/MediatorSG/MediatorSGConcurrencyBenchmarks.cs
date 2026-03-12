// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Isolated martinothamar/Mediator-only benchmark: concurrent and sequential load.
/// <para>
/// <b>FanOut</b> — fires <see cref="ConcurrentTasks"/> parallel Send() calls via
/// <c>Task.WhenAll</c>. MediatorSG returns <c>ValueTask&lt;T&gt;</c>, so each call
/// pays an extra <c>.AsTask()</c> conversion.
/// </para>
/// <para>
/// <b>Throughput</b> — fires <see cref="ConcurrentTasks"/> sequential <c>await</c> calls.
/// No <c>.AsTask()</c> conversion — measures pure mediator dispatch overhead per call.
/// </para>
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MediatorSGConcurrencyBenchmarks
{
    private const int ConcurrentTasks = 100;

    private PingMediatorSGHandler _handler = null!;
    private global::Mediator.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    private Task<int>[] _tasks = null!;
    private PingMediatorSG[] _requests = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handler = new PingMediatorSGHandler();

        _tasks = new Task<int>[ConcurrentTasks];
        _requests = new PingMediatorSG[ConcurrentTasks];

        for (int i = 0; i < ConcurrentTasks; i++)
            _requests[i] = new PingMediatorSG();

        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();

        // Warmup
        _ = _mediator.Send(new PingMediatorSG()).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    // ══════════════════════════════════════════════════════════════
    // FanOut — Task.WhenAll pattern (includes .AsTask() cost for ValueTask APIs)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Baseline: direct handler call fan-out via <c>Task.WhenAll</c>.
    /// Pays <c>.AsTask()</c> because the handler returns <c>ValueTask&lt;int&gt;</c>.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FanOut")]
    public async Task Direct_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _handler.Handle(_requests[i], default).AsTask();

        await Task.WhenAll(_tasks);
    }

    /// <summary>
    /// MediatorSG fan-out: returns <c>ValueTask&lt;int&gt;</c> — requires <c>.AsTask()</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task MediatorSG_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _mediator.Send(_requests[i]).AsTask();

        await Task.WhenAll(_tasks);
    }

    // ══════════════════════════════════════════════════════════════
    // Throughput
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Baseline: direct handler call × N, pure <c>await</c>.
    /// No <c>.AsTask()</c> — zero allocation path.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Throughput")]
    public async Task<int> Direct_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _handler.Handle(_requests[i], default);
        return sum;
    }

    /// <summary>
    /// MediatorSG throughput: pure <c>await</c> on <c>ValueTask&lt;int&gt;</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<int> MediatorSG_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _mediator.Send(_requests[i]);
        return sum;
    }
}
