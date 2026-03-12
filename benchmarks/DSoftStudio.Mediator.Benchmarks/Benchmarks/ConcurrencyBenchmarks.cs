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
/// Measures mediator performance under concurrent and sequential load.
/// <para>
/// <b>FanOut</b> — fires <see cref="ConcurrentTasks"/> parallel Send() calls via
/// <c>Task.WhenAll</c>. DSoft pays an extra <c>.AsTask()</c> conversion because
/// <c>ValueTask&lt;T&gt;</c> cannot be used with <c>Task.WhenAll</c>.
/// MediatR returns <c>Task&lt;T&gt;</c> natively, so no conversion is needed.
/// This represents a real fan-out/scatter-gather pattern.
/// </para>
/// <para>
/// <b>Throughput</b> — fires <see cref="ConcurrentTasks"/> sequential <c>await</c> calls.
/// No <c>.AsTask()</c> conversion — measures pure mediator dispatch overhead per call.
/// This isolates the actual Send() cost without the <c>ValueTask→Task</c> tax.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ConcurrencyBenchmarks
{
    private const int ConcurrentTasks = 100;

    private PingHandler _handler = null!;
    private IMediator _dsoft = null!;
    private MediatR.IMediator _mediatr = null!;
    private DispatchR.IMediator _dispatchr = null!;
    private global::Mediator.IMediator _mediatorsg = null!;

    private IServiceScope _dsoftScope = null!;
    private IServiceScope _mediatrScope = null!;
    private IServiceScope _dispatchrScope = null!;
    private IServiceScope _mediatorsgScope = null!;

    private Task<int>[] _tasks = null!;
    private Ping[] _requests = null!;
    private PingMediatR[] _mediatrRequests = null!;
    private PingDispatchR[] _dispatchrRequests = null!;
    private PingMediatorSG[] _mediatorsgRequests = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handler = new PingHandler();

        _tasks = new Task<int>[ConcurrentTasks];
        _requests = new Ping[ConcurrentTasks];
        _mediatrRequests = new PingMediatR[ConcurrentTasks];
        _dispatchrRequests = new PingDispatchR[ConcurrentTasks];
        _mediatorsgRequests = new PingMediatorSG[ConcurrentTasks];

        for (int i = 0; i < ConcurrentTasks; i++)
        {
            _requests[i] = new Ping();
            _mediatrRequests[i] = new PingMediatR();
            _dispatchrRequests[i] = new PingDispatchR();
            _mediatorsgRequests[i] = new PingMediatorSG();
        }

        // ── DSoftStudio Mediator ──────────────────────────────────
        {
            var services = new ServiceCollection();

            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers()
                .PrecompilePipelines();

            var provider = services.BuildServiceProvider();
            _dsoftScope = provider.CreateScope();
            _dsoft = _dsoftScope.ServiceProvider.GetRequiredService<IMediator>();
        }

        // ── MediatR 14.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

            var provider = services.BuildServiceProvider();
            _mediatrScope = provider.CreateScope();
            _mediatr = _mediatrScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── DispatchR 2.x ──────────────────────────────────────────
        {
            var services = new ServiceCollection();

            services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

            var provider = services.BuildServiceProvider();
            _dispatchrScope = provider.CreateScope();
            _dispatchr = _dispatchrScope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // ── martinothamar/Mediator (source-generated) ──────────────────
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);

            var provider = services.BuildServiceProvider();
            _mediatorsgScope = provider.CreateScope();
            _mediatorsg = _mediatorsgScope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        }

        // Warmup all mediators
        _ = _dsoft.Send<Ping, int>(new Ping()).GetAwaiter().GetResult();
        _ = _mediatr.Send(new PingMediatR()).GetAwaiter().GetResult();
        _ = _dispatchr.Send<PingDispatchR, ValueTask<int>>(new PingDispatchR(), default).GetAwaiter().GetResult();
        _ = _mediatorsg.Send(new PingMediatorSG()).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _dsoftScope?.Dispose();
        _mediatrScope?.Dispose();
        _dispatchrScope?.Dispose();
        _mediatorsgScope?.Dispose();
    }

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
    /// allocates a <c>Task&lt;int&gt;</c> (~72 B × N). This is the cost of using
    /// <c>ValueTask</c> APIs with <c>Task.WhenAll</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task DSoft_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _dsoft.Send<Ping, int>(_requests[i]).AsTask();

        await Task.WhenAll(_tasks);
    }

    /// <summary>
    /// MediatR fan-out: returns <c>Task&lt;int&gt;</c> natively — no conversion needed.
    /// <c>Task.FromResult</c> caches small integers, so no allocation per call.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task MediatR_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _mediatr.Send(_mediatrRequests[i]);

        await Task.WhenAll(_tasks);
    }

    /// <summary>
    /// DispatchR fan-out: returns <c>ValueTask&lt;int&gt;</c> — requires <c>.AsTask()</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task DispatchR_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _dispatchr.Send<PingDispatchR, ValueTask<int>>(_dispatchrRequests[i], default).AsTask();

        await Task.WhenAll(_tasks);
    }

    /// <summary>
    /// martinothamar/Mediator fan-out: returns <c>ValueTask&lt;int&gt;</c> — requires <c>.AsTask()</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FanOut")]
    public async Task MediatorSG_FanOut()
    {
        for (int i = 0; i < ConcurrentTasks; i++)
            _tasks[i] = _mediatorsg.Send(_mediatorsgRequests[i]).AsTask();

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
            sum += await _dsoft.Send<Ping, int>(_requests[i]);
        return sum;
    }

    /// <summary>
    /// MediatR throughput: pure <c>await</c> on <c>Task&lt;int&gt;</c>.
    /// Apples-to-apples with DSoft — both just await, no conversion overhead.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<int> MediatR_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _mediatr.Send(_mediatrRequests[i]);
        return sum;
    }

    /// <summary>
    /// DispatchR throughput: pure <c>await</c> on <c>ValueTask&lt;int&gt;</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<int> DispatchR_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _dispatchr.Send<PingDispatchR, ValueTask<int>>(_dispatchrRequests[i], default);
        return sum;
    }

    /// <summary>
    /// martinothamar/Mediator throughput: pure <c>await</c> on <c>ValueTask&lt;int&gt;</c>.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Throughput")]
    public async Task<int> MediatorSG_Throughput()
    {
        int sum = 0;
        for (int i = 0; i < ConcurrentTasks; i++)
            sum += await _mediatorsg.Send(_mediatorsgRequests[i]);
        return sum;
    }
}