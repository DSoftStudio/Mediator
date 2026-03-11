// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Isolated DispatchR-only benchmark: Send with 0 / 3 / 5 behaviors.
/// No other mediator libraries — avoids static dispatch table contamination.
/// Uses counting behaviors to verify the pipeline chain actually executes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRSendBenchmarks
{
    private static readonly PingDispatchR Message = new();

    private DispatchR.IMediator _noBehaviors = null!;
    private DispatchR.IMediator _threeBehaviors = null!;
    private DispatchR.IMediator _fiveBehaviors = null!;
    private IServiceScope _scope0 = null!;
    private IServiceScope _scope3 = null!;
    private IServiceScope _scope5 = null!;

    // ── Pass-through behaviors with counting ──────────────────────

    private sealed class Behavior1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return NextPipeline.Handle(request, ct);
        }
    }

    private sealed class Behavior2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return NextPipeline.Handle(request, ct);
        }
    }

    private sealed class Behavior3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return NextPipeline.Handle(request, ct);
        }
    }

    private sealed class Behavior4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return NextPipeline.Handle(request, ct);
        }
    }

    private sealed class Behavior5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return NextPipeline.Handle(request, ct);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        // ── No behaviors ─────────────────────────────────────────
        {
            var services = new ServiceCollection();
            services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: false, withNotifications: false);

            var provider = services.BuildServiceProvider();
            _scope0 = provider.CreateScope();
            _noBehaviors = _scope0.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // ── 3 behaviors ──────────────────────────────────────────
        {
            var services = new ServiceCollection();
            services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: true, withNotifications: false);

            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior1));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior2));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior3));

            var provider = services.BuildServiceProvider();
            _scope3 = provider.CreateScope();
            _threeBehaviors = _scope3.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // ── 5 behaviors ──────────────────────────────────────────
        {
            var services = new ServiceCollection();
            services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: true, withNotifications: false);

            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior1));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior2));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior3));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior4));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(Behavior5));

            var provider = services.BuildServiceProvider();
            _scope5 = provider.CreateScope();
            _fiveBehaviors = _scope5.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // ── Warmup ───────────────────────────────────────────────
        _noBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default).GetAwaiter().GetResult();
        _threeBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default).GetAwaiter().GetResult();
        _fiveBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default).GetAwaiter().GetResult();

        // ── Verification ─────────────────────────────────────────
        Behavior1.CallCount = 0;
        Behavior2.CallCount = 0;
        Behavior3.CallCount = 0;
        Behavior4.CallCount = 0;
        Behavior5.CallCount = 0;

        _fiveBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default).GetAwaiter().GetResult();

        int total = Behavior1.CallCount
                  + Behavior2.CallCount
                  + Behavior3.CallCount
                  + Behavior4.CallCount
                  + Behavior5.CallCount;

        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  DISPATCHR PIPELINE VERIFICATION                        ║");
        Console.WriteLine($"  ║  Behavior 1: {Behavior1.CallCount} call(s)                                   ║");
        Console.WriteLine($"  ║  Behavior 2: {Behavior2.CallCount} call(s)                                   ║");
        Console.WriteLine($"  ║  Behavior 3: {Behavior3.CallCount} call(s)                                   ║");
        Console.WriteLine($"  ║  Behavior 4: {Behavior4.CallCount} call(s)                                   ║");
        Console.WriteLine($"  ║  Behavior 5: {Behavior5.CallCount} call(s)                                   ║");
        Console.WriteLine($"  ║  Total: {total}/5 behaviors fired                            ║");
        Console.WriteLine($"  ║  Status: {(total == 5 ? "✓ ALL BEHAVIORS EXECUTING" : "✗ BEHAVIORS NOT RUNNING!")}          ║");
        Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Reset for benchmark
        Behavior1.CallCount = 0;
        Behavior2.CallCount = 0;
        Behavior3.CallCount = 0;
        Behavior4.CallCount = 0;
        Behavior5.CallCount = 0;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope0?.Dispose();
        _scope3?.Dispose();
        _scope5?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> DispatchR_Send()
        => await _noBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default);

    [Benchmark]
    public async Task<int> DispatchR_Send_3Behaviors()
        => await _threeBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default);

    [Benchmark]
    public async Task<int> DispatchR_Send_5Behaviors()
        => await _fiveBehaviors.Send<PingDispatchR, ValueTask<int>>(Message, default);
}
