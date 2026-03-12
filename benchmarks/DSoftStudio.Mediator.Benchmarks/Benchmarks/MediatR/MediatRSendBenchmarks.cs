// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

/// <summary>
/// Isolated MediatR-only benchmark: Send with 0 / 3 / 5 behaviors.
/// No other mediator libraries — avoids static dispatch table contamination.
/// Uses counting behaviors to verify the pipeline chain actually executes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatRSendBenchmarks
{
    private static readonly PingMediatR Message = new();

    private MediatR.IMediator _noBehaviors = null!;
    private MediatR.IMediator _threeBehaviors = null!;
    private MediatR.IMediator _fiveBehaviors = null!;
    private IServiceScope _scope0 = null!;
    private IServiceScope _scope3 = null!;
    private IServiceScope _scope5 = null!;

    // ── Pass-through behaviors ────────────────────────────────────

    private sealed class Behavior1 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public static int CallCount;
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return next();
        }
    }

    private sealed class Behavior2 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public static int CallCount;
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return next();
        }
    }

    private sealed class Behavior3 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public static int CallCount;
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return next();
        }
    }

    private sealed class Behavior4 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public static int CallCount;
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return next();
        }
    }

    private sealed class Behavior5 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public static int CallCount;
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return next();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        // ── No behaviors ─────────────────────────────────────────
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

            var provider = services.BuildServiceProvider();
            _scope0 = provider.CreateScope();
            _noBehaviors = _scope0.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── 3 behaviors ──────────────────────────────────────────
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior1));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior2));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior3));

            var provider = services.BuildServiceProvider();
            _scope3 = provider.CreateScope();
            _threeBehaviors = _scope3.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── 5 behaviors ──────────────────────────────────────────
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior1));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior2));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior3));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior4));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(Behavior5));

            var provider = services.BuildServiceProvider();
            _scope5 = provider.CreateScope();
            _fiveBehaviors = _scope5.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── Warmup ───────────────────────────────────────────────
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
        Console.WriteLine("  ║  MEDIATR PIPELINE VERIFICATION                          ║");
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
        => await new PingMediatRHandler().Handle(Message, default);

    [Benchmark]
    public async Task<int> MediatR_Send()
        => await _noBehaviors.Send(Message);

    [Benchmark]
    public async Task<int> MediatR_Send_3Behaviors()
        => await _threeBehaviors.Send(Message);

    [Benchmark]
    public async Task<int> MediatR_Send_5Behaviors()
        => await _fiveBehaviors.Send(Message);
}
