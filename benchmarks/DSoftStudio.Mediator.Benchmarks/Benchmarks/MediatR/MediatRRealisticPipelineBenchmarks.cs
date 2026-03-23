// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Realistic enterprise pipeline: Validation → Logging → Handler(async DB).
/// Isolated MediatR-only — no other mediator libraries loaded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatRRealisticPipelineBenchmarks
{
    private static readonly CreateOrderCommandMediatR Command = new("Widget", 5);

    private MediatR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    // ── Realistic behaviors ───────────────────────────────────────

    private sealed class ValidationStep : MediatR.IPipelineBehavior<CreateOrderCommandMediatR, OrderResult>
    {
        public static int CallCount;

        public Task<OrderResult> Handle(
            CreateOrderCommandMediatR request,
            MediatR.RequestHandlerDelegate<OrderResult> next,
            CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);

            if (string.IsNullOrEmpty(request.Product) || request.Quantity <= 0)
                return Task.FromResult(new OrderResult(-1, false));

            return next();
        }
    }

    private sealed class LoggingStep : MediatR.IPipelineBehavior<CreateOrderCommandMediatR, OrderResult>
    {
        public static int CallCount;

        public Task<OrderResult> Handle(
            CreateOrderCommandMediatR request,
            MediatR.RequestHandlerDelegate<OrderResult> next,
            CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);

            _ = request.Product;
            _ = request.Quantity;

            return next();
        }
    }

    private sealed class MetricsStep : MediatR.IPipelineBehavior<CreateOrderCommandMediatR, OrderResult>
    {
        public static int CallCount;

        public Task<OrderResult> Handle(
            CreateOrderCommandMediatR request,
            MediatR.RequestHandlerDelegate<OrderResult> next,
            CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return next();
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CreateOrderMediatRHandler).Assembly));

        services.AddSingleton<FakeOrderRepository>();
        services.AddTransient(typeof(MediatR.IPipelineBehavior<CreateOrderCommandMediatR, OrderResult>), typeof(ValidationStep));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<CreateOrderCommandMediatR, OrderResult>), typeof(LoggingStep));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<CreateOrderCommandMediatR, OrderResult>), typeof(MetricsStep));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

        // ── Verification ─────────────────────────────────────────
        ValidationStep.CallCount = 0;
        LoggingStep.CallCount = 0;
        MetricsStep.CallCount = 0;

        var result = _mediator.Send(Command).GetAwaiter().GetResult();

        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  MEDIATR REALISTIC PIPELINE VERIFICATION                 ║");
        Console.WriteLine($"  ║  Validation: {ValidationStep.CallCount} call(s)                                    ║");
        Console.WriteLine($"  ║  Logging:    {LoggingStep.CallCount} call(s)                                    ║");
        Console.WriteLine($"  ║  Metrics:    {MetricsStep.CallCount} call(s)                                    ║");
        Console.WriteLine($"  ║  DB Result:  OrderId={result.OrderId}, Saved={result.Saved}               ║");
        Console.WriteLine($"  ║  Status: {(ValidationStep.CallCount == 1 && LoggingStep.CallCount == 1 && MetricsStep.CallCount == 1 && result.Saved ? "✓ FULL PIPELINE OK" : "✗ PIPELINE BROKEN!")}                       ║");
        Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Reset for benchmark
        ValidationStep.CallCount = 0;
        LoggingStep.CallCount = 0;
        MetricsStep.CallCount = 0;

        // Warmup
        _mediator.Send(Command).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _scope?.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<OrderResult> DirectCall_WithPipeline()
    {
        var product = Command.Product;
        var quantity = Command.Quantity;

        if (string.IsNullOrEmpty(product) || quantity <= 0)
            return new OrderResult(-1, false);

        _ = product;
        _ = quantity;

        return await new FakeOrderRepository().SaveAsync(product, quantity, default);
    }

    [Benchmark]
    public async Task<OrderResult> MediatR_RealisticPipeline()
        => await _mediator.Send(Command);
}
