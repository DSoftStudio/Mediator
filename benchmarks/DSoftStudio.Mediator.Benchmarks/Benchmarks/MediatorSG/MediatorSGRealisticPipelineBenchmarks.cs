// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Realistic enterprise pipeline: Validation → Logging → Handler(async DB).
/// Isolated martinothamar/Mediator-only — no other mediator libraries loaded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MediatorSGRealisticPipelineBenchmarks
{
    private static readonly CreateOrderCommandMediatorSG Command = new("Widget", 5);

    private global::Mediator.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    // ── Realistic behaviors ───────────────────────────────────────

    private sealed class ValidationStep : global::Mediator.IPipelineBehavior<CreateOrderCommandMediatorSG, OrderResult>
    {
        public static int CallCount;

        public ValueTask<OrderResult> Handle(
            CreateOrderCommandMediatorSG message,
            global::Mediator.MessageHandlerDelegate<CreateOrderCommandMediatorSG, OrderResult> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);

            if (string.IsNullOrEmpty(message.Product) || message.Quantity <= 0)
                return new ValueTask<OrderResult>(new OrderResult(-1, false));

            return next(message, cancellationToken);
        }
    }

    private sealed class LoggingStep : global::Mediator.IPipelineBehavior<CreateOrderCommandMediatorSG, OrderResult>
    {
        public static int CallCount;

        public ValueTask<OrderResult> Handle(
            CreateOrderCommandMediatorSG message,
            global::Mediator.MessageHandlerDelegate<CreateOrderCommandMediatorSG, OrderResult> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);

            _ = message.Product;
            _ = message.Quantity;

            return next(message, cancellationToken);
        }
    }

    private sealed class MetricsStep : global::Mediator.IPipelineBehavior<CreateOrderCommandMediatorSG, OrderResult>
    {
        public static int CallCount;

        public ValueTask<OrderResult> Handle(
            CreateOrderCommandMediatorSG message,
            global::Mediator.MessageHandlerDelegate<CreateOrderCommandMediatorSG, OrderResult> next,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return next(message, cancellationToken);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        MediatorSGHelper.AddMediatorSG(services);

        services.AddSingleton<FakeOrderRepository>();
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<CreateOrderCommandMediatorSG, OrderResult>), typeof(ValidationStep));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<CreateOrderCommandMediatorSG, OrderResult>), typeof(LoggingStep));
        services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<CreateOrderCommandMediatorSG, OrderResult>), typeof(MetricsStep));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();

        // ── Verification ─────────────────────────────────────────
        ValidationStep.CallCount = 0;
        LoggingStep.CallCount = 0;
        MetricsStep.CallCount = 0;

        var result = _mediator.Send(Command).GetAwaiter().GetResult();

        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  MEDIATOR-SG REALISTIC PIPELINE VERIFICATION             ║");
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
    public async Task<OrderResult> MediatorSG_RealisticPipeline()
        => await _mediator.Send(Command);
}
