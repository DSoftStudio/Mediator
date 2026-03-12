// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

/// <summary>
/// Isolated MediatR-only benchmark: concurrent and sequential load.
/// <para>
/// <b>FanOut</b> — fires <see cref="ConcurrentTasks"/> parallel Send() calls via
/// <c>Task.WhenAll</c>. MediatR returns <c>Task&lt;T&gt;</c> natively — no
/// <c>.AsTask()</c> conversion needed.
/// </para>
/// <para>
/// <b>Throughput</b> — fires <see cref="ConcurrentTasks"/> sequential <c>await</c> calls.
/// Measures pure mediator dispatch overhead per call.
/// </para>
/// Separate class = separate BenchmarkDotNet process — zero static dispatch contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MediatRConcurrencyBenchmarks
{
    private const int ConcurrentTasks = 100;

    private PingMediatRHandler _handler = null!;
    private MediatR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    private Task<int>[] _tasks = null!;
    private PingMediatR[] _requests = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handler = new PingMediatRHandler();

        _tasks = new Task<int>[ConcurrentTasks];
        _requests = new PingMediatR[ConcurrentTasks];

        for (int i = 0; i < ConcurrentTasks; i++)
            _requests[i] = new PingMediatR();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

        // Warmup
        _ = _mediator.Send(new PingMediatR()).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    // ══════════════════════════════════════════════════════════════
    // FanOut — Task.WhenAll pattern
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Baseline: direct handler call fan-out via <c>Task.WhenAll</c>.
    /// MediatR handler returns <c>Task&lt;int&gt;</c> — no conversion needed.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FanOut")]
    public async Task Direct_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _handler.Handle(_requests[i], default);

        await Task.WhenAll(_tasks);
    }

    /// <summary>
    /// MediatR fan-out: returns <c>Task&lt;int&gt;</c> natively — no conversion needed.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task MediatR_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _mediator.Send(_requests[i]);

        await Task.WhenAll(_tasks);
    }

    // ══════════════════════════════════════════════════════════════
    // Throughput
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Baseline: direct handler call × N, pure <c>await</c>.
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
    /// MediatR throughput: pure <c>await</c> on <c>Task&lt;int&gt;</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<int> MediatR_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _mediator.Send(_requests[i]);
        return sum;
    }
}
