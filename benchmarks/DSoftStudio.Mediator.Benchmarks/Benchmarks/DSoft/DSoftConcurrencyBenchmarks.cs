// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Isolated DSoft-only benchmark: concurrent and sequential load.
/// <para>
/// <b>FanOut</b> — fires <see cref="ConcurrentTasks"/> parallel Send() calls via
/// <c>Task.WhenAll</c>. DSoft pays an extra <c>.AsTask()</c> conversion because
/// <c>ValueTask&lt;T&gt;</c> cannot be used with <c>Task.WhenAll</c>.
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
public class DSoftConcurrencyBenchmarks
{
    private const int ConcurrentTasks = 100;

    private PingHandler _handler = null!;
    private IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    private Task<int>[] _tasks = null!;
    private Ping[] _requests = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handler = new PingHandler();

        _tasks = new Task<int>[ConcurrentTasks];
        _requests = new Ping[ConcurrentTasks];

        for (int i = 0; i < ConcurrentTasks; i++)
            _requests[i] = new Ping();

        var services = new ServiceCollection();
        DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();

        // Warmup
        _ = _mediator.Send<Ping, int>(new Ping()).GetAwaiter().GetResult();
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
    /// DSoft fan-out: <c>.AsTask()</c> on each synchronous <c>ValueTask&lt;int&gt;</c>
    /// allocates a <c>Task&lt;int&gt;</c> (~72 B × N).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task DSoft_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _mediator.Send<Ping, int>(_requests[i]).AsTask();

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
    /// DSoft throughput: pure <c>await</c> on <c>ValueTask&lt;int&gt;</c>.
    /// No <c>.AsTask()</c> conversion — measures actual mediator dispatch cost.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<int> DSoft_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _mediator.Send<Ping, int>(_requests[i]);
        return sum;
    }
}
