// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Isolated DSoft-only benchmark: Send with 3 / 5 behaviors.
/// No other mediator libraries — avoids static dispatch table contamination.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DSoftSendBenchmarks
{
    private static readonly PingWithPipeline PingWithPipelineMessage = new();

    private PingHandler _directHandler = null!;
    private IMediator _threeBehaviors = null!;
    private IMediator _fiveBehaviors = null!;

    private IServiceScope _scope3 = null!;
    private IServiceScope _scope5 = null!;

    // ── Counting behavior for verification ────────────────────────

    private sealed class CountingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public static int CallCount;

        public ValueTask<TResponse> Handle(
            TRequest request,
            IRequestHandler<TRequest, TResponse> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next.Handle(request, cancellationToken);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingHandler();

        // ── 5 behaviors FIRST (must initialize RequestDispatch<PingWithPipeline, int>
        // before 3-behaviors setup, because TryInitialize is write-once static) ──
        {
            var services = new ServiceCollection();
            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers();

            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(LoggingBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(ValidationBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(MetricsBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(AuthorizationBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(TransactionBehavior<PingWithPipeline, int>));

            // Counting behavior for verification ONLY
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(CountingBehavior<PingWithPipeline, int>));

            services.PrecompilePipelines();

            // Verify behaviors execute
            var verifyProvider = services.BuildServiceProvider();
            using var verifyScope = verifyProvider.CreateScope();
            var verifyMediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();
            CountingBehavior<PingWithPipeline, int>.CallCount = 0;
            verifyMediator.Send<PingWithPipeline, int>(PingWithPipelineMessage).GetAwaiter().GetResult();
            var count = CountingBehavior<PingWithPipeline, int>.CallCount;
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  DSOFT PIPELINE VERIFICATION                             ║");
            Console.WriteLine($"  ║  CountingBehavior called: {count} time(s)                     ║");
            Console.WriteLine($"  ║  Status: {(count == 1 ? "✓ ALL BEHAVIORS EXECUTING" : "✗ BEHAVIORS NOT RUNNING!")}            ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        // Build REAL benchmark provider with exactly 5 behaviors (no counter).
        {
            var services = new ServiceCollection();
            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers();

            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(LoggingBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(ValidationBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(MetricsBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(AuthorizationBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(TransactionBehavior<PingWithPipeline, int>));

            services.PrecompilePipelines();

            var provider = services.BuildServiceProvider();
            _scope5 = provider.CreateScope();
            _fiveBehaviors = _scope5.ServiceProvider.GetRequiredService<IMediator>();
        }

        // ── 3 behaviors ──────────────────────────────────────────────
        {
            var services = new ServiceCollection();
            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers();

            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(LoggingBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(ValidationBehavior<PingWithPipeline, int>));
            services.AddScoped(typeof(IPipelineBehavior<PingWithPipeline, int>), typeof(MetricsBehavior<PingWithPipeline, int>));

            services.PrecompilePipelines();

            var provider = services.BuildServiceProvider();
            _scope3 = provider.CreateScope();
            _threeBehaviors = _scope3.ServiceProvider.GetRequiredService<IMediator>();
        }

        // Warmup
        _directHandler.Handle(new Ping(), default).GetAwaiter().GetResult();
        _threeBehaviors.Send<PingWithPipeline, int>(PingWithPipelineMessage).GetAwaiter().GetResult();
        _fiveBehaviors.Send<PingWithPipeline, int>(PingWithPipelineMessage).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope3?.Dispose();
        _scope5?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> DirectCall()
        => await _directHandler.Handle(new Ping(), default);

    [Benchmark]
    public async Task<int> DSoft_Send_3Behaviors()
        => await _threeBehaviors.Send<PingWithPipeline, int>(PingWithPipelineMessage);

    [Benchmark]
    public async Task<int> DSoft_Send_5Behaviors()
        => await _fiveBehaviors.Send<PingWithPipeline, int>(PingWithPipelineMessage);
}
