// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Isolated martinothamar/Mediator-only benchmark: Send with 0 / 3 / 5 behaviors.
/// No other mediator libraries — avoids static dispatch table contamination.
/// Uses counting behaviors to verify the pipeline chain actually executes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatorSGSendBenchmarks
{
    private static readonly PingMediatorSG Message = new();

    private PingMediatorSGHandler _directHandler = null!;
    private global::Mediator.IMediator _noBehaviors = null!;
    private global::Mediator.IMediator _threeBehaviors = null!;
    private global::Mediator.IMediator _fiveBehaviors = null!;
    private IServiceScope _scope0 = null!;
    private IServiceScope _scope3 = null!;
    private IServiceScope _scope5 = null!;

    // ── Pass-through behaviors with counting ──────────────────────

    private sealed class Behavior1 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public static int CallCount;
        public ValueTask<int> Handle(
            PingMediatorSG message,
            global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next(message, cancellationToken);
        }
    }

    private sealed class Behavior2 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public static int CallCount;
        public ValueTask<int> Handle(
            PingMediatorSG message,
            global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next(message, cancellationToken);
        }
    }

    private sealed class Behavior3 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public static int CallCount;
        public ValueTask<int> Handle(
            PingMediatorSG message,
            global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next(message, cancellationToken);
        }
    }

    private sealed class Behavior4 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public static int CallCount;
        public ValueTask<int> Handle(
            PingMediatorSG message,
            global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next(message, cancellationToken);
        }
    }

    private sealed class Behavior5 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public static int CallCount;
        public ValueTask<int> Handle(
            PingMediatorSG message,
            global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next(message, cancellationToken);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingMediatorSGHandler();

        // ── No behaviors
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);

            var provider = services.BuildServiceProvider();
            _scope0 = provider.CreateScope();
            _noBehaviors = _scope0.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        }

        // ── 3 behaviors ──────────────────────────────────────────
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);

            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior1));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior2));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior3));

            var provider = services.BuildServiceProvider();
            _scope3 = provider.CreateScope();
            _threeBehaviors = _scope3.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        }

        // ── 5 behaviors ──────────────────────────────────────────
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);

            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior1));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior2));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior3));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior4));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(Behavior5));

            var provider = services.BuildServiceProvider();
            _scope5 = provider.CreateScope();
            _fiveBehaviors = _scope5.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        }

        // ── Warmup ───────────────────────────────────────────────
        _directHandler.Handle(Message, default).GetAwaiter().GetResult();
        _noBehaviors.Send(Message).GetAwaiter().GetResult();
        _threeBehaviors.Send(Message).GetAwaiter().GetResult();
        _fiveBehaviors.Send(Message).GetAwaiter().GetResult();

        // ── Verification ─────────────────────────────────────────
        Behavior1.CallCount = 0;
        Behavior2.CallCount = 0;
        Behavior3.CallCount = 0;
        Behavior4.CallCount = 0;
        Behavior5.CallCount = 0;

        _fiveBehaviors.Send(Message).GetAwaiter().GetResult();

        int total = Behavior1.CallCount
                  + Behavior2.CallCount
                  + Behavior3.CallCount
                  + Behavior4.CallCount
                  + Behavior5.CallCount;

        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  MEDIATOR-SG PIPELINE VERIFICATION                      ║");
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
    public async Task<int> DirectCall()
        => await _directHandler.Handle(Message, default);

    [Benchmark]
    public async Task<int> MediatorSG_Send()
        => await _noBehaviors.Send(Message);

    [Benchmark]
    public async Task<int> MediatorSG_Send_3Behaviors()
        => await _threeBehaviors.Send(Message);

    [Benchmark]
    public async Task<int> MediatorSG_Send_5Behaviors()
        => await _fiveBehaviors.Send(Message);
}
