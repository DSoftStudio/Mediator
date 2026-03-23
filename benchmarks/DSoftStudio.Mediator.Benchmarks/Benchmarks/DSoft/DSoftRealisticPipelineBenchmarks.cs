// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

/// <summary>
/// Realistic enterprise pipeline: Validation → Logging → Handler(async DB).
/// Isolated DSoft-only — no other mediator libraries loaded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DSoftRealisticPipelineBenchmarks
{
    private static readonly CreateOrderCommand Command = new("Widget", 5);

    private IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    // ── Realistic behaviors ───────────────────────────────────────

    private sealed class ValidationStep : IPipelineBehavior<CreateOrderCommand, OrderResult>
    {
        public static int CallCount;

        public ValueTask<OrderResult> Handle(
            CreateOrderCommand request,
            IRequestHandler<CreateOrderCommand, OrderResult> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);

            // Simulate validation: check fields are valid
            if (string.IsNullOrEmpty(request.Product) || request.Quantity <= 0)
                return new ValueTask<OrderResult>(new OrderResult(-1, false));

            return next.Handle(request, cancellationToken);
        }
    }

    private sealed class LoggingStep : IPipelineBehavior<CreateOrderCommand, OrderResult>
    {
        public static int CallCount;

        public ValueTask<OrderResult> Handle(
            CreateOrderCommand request,
            IRequestHandler<CreateOrderCommand, OrderResult> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);

            // Simulate logging: capture request info (no real I/O)
            _ = request.Product;
            _ = request.Quantity;

            return next.Handle(request, cancellationToken);
        }
    }

    private sealed class MetricsStep : IPipelineBehavior<CreateOrderCommand, OrderResult>
    {
        public static int CallCount;

        public ValueTask<OrderResult> Handle(
            CreateOrderCommand request,
            IRequestHandler<CreateOrderCommand, OrderResult> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);

            // Simulate metrics collection
            return next.Handle(request, cancellationToken);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        // ── Verify pipeline executes ─────────────────────────────
        {
            var services = new ServiceCollection();
            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers();

            services.AddSingleton<FakeOrderRepository>();
            services.AddScoped(typeof(IPipelineBehavior<CreateOrderCommand, OrderResult>), typeof(ValidationStep));
            services.AddScoped(typeof(IPipelineBehavior<CreateOrderCommand, OrderResult>), typeof(LoggingStep));
            services.AddScoped(typeof(IPipelineBehavior<CreateOrderCommand, OrderResult>), typeof(MetricsStep));

            services.PrecompilePipelines();

            var verifyProvider = services.BuildServiceProvider();
            using var verifyScope = verifyProvider.CreateScope();
            var verifyMediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();

            ValidationStep.CallCount = 0;
            LoggingStep.CallCount = 0;
            MetricsStep.CallCount = 0;

            var result = verifyMediator.Send<CreateOrderCommand, OrderResult>(Command).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  DSOFT REALISTIC PIPELINE VERIFICATION                   ║");
            Console.WriteLine($"  ║  Validation: {ValidationStep.CallCount} call(s)                                    ║");
            Console.WriteLine($"  ║  Logging:    {LoggingStep.CallCount} call(s)                                    ║");
            Console.WriteLine($"  ║  Metrics:    {MetricsStep.CallCount} call(s)                                    ║");
            Console.WriteLine($"  ║  DB Result:  OrderId={result.OrderId}, Saved={result.Saved}               ║");
            Console.WriteLine($"  ║  Status: {(ValidationStep.CallCount == 1 && LoggingStep.CallCount == 1 && MetricsStep.CallCount == 1 && result.Saved ? "✓ FULL PIPELINE OK" : "✗ PIPELINE BROKEN!")}                       ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        // ── Build benchmark provider ─────────────────────────────
        {
            var services = new ServiceCollection();
            DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator(services)
                .RegisterMediatorHandlers();

            services.AddSingleton<FakeOrderRepository>();
            services.AddScoped(typeof(IPipelineBehavior<CreateOrderCommand, OrderResult>), typeof(ValidationStep));
            services.AddScoped(typeof(IPipelineBehavior<CreateOrderCommand, OrderResult>), typeof(LoggingStep));
            services.AddScoped(typeof(IPipelineBehavior<CreateOrderCommand, OrderResult>), typeof(MetricsStep));

            services.PrecompilePipelines();

            var provider = services.BuildServiceProvider();
            _scope = provider.CreateScope();
            _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        }

        // Warmup
        _mediator.Send<CreateOrderCommand, OrderResult>(Command).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<OrderResult> DirectCall_WithPipeline()
    {
        // Same work as Validation → Logging → Metrics → Handler, no framework
        var product = Command.Product;
        var quantity = Command.Quantity;

        // Validation
        if (string.IsNullOrEmpty(product) || quantity <= 0)
            return new OrderResult(-1, false);

        // Logging
        _ = product;
        _ = quantity;

        // Metrics (noop)

        // Handler → DB
        return await new FakeOrderRepository().SaveAsync(product, quantity, default);
    }

    [Benchmark]
    public async Task<OrderResult> DSoft_RealisticPipeline()
        => await _mediator.Send<CreateOrderCommand, OrderResult>(Command);
}
