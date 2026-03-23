// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using DispatchR.Extensions;

namespace Benchmarks;

/// <summary>
/// Realistic enterprise pipeline: Validation → Logging → Handler(async DB).
/// Isolated DispatchR-only — no other mediator libraries loaded.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DispatchRRealisticPipelineBenchmarks
{
    private static readonly CreateOrderCommandDispatchR Command = new() { Product = "Widget", Quantity = 5 };

    private DispatchR.IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    // ── Realistic behaviors ───────────────────────────────────────

    private sealed class ValidationStep
        : global::DispatchR.Abstractions.Send.IPipelineBehavior<CreateOrderCommandDispatchR, ValueTask<OrderResult>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<CreateOrderCommandDispatchR, ValueTask<OrderResult>> NextPipeline { get; set; }

        public ValueTask<OrderResult> Handle(CreateOrderCommandDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);

            if (string.IsNullOrEmpty(request.Product) || request.Quantity <= 0)
                return new ValueTask<OrderResult>(new OrderResult(-1, false));

            return NextPipeline.Handle(request, ct);
        }
    }

    private sealed class LoggingStep
        : global::DispatchR.Abstractions.Send.IPipelineBehavior<CreateOrderCommandDispatchR, ValueTask<OrderResult>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<CreateOrderCommandDispatchR, ValueTask<OrderResult>> NextPipeline { get; set; }

        public ValueTask<OrderResult> Handle(CreateOrderCommandDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);

            _ = request.Product;
            _ = request.Quantity;

            return NextPipeline.Handle(request, ct);
        }
    }

    private sealed class MetricsStep
        : global::DispatchR.Abstractions.Send.IPipelineBehavior<CreateOrderCommandDispatchR, ValueTask<OrderResult>>
    {
        public static int CallCount;
        public required global::DispatchR.Abstractions.Send.IRequestHandler<CreateOrderCommandDispatchR, ValueTask<OrderResult>> NextPipeline { get; set; }

        public ValueTask<OrderResult> Handle(CreateOrderCommandDispatchR request, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return NextPipeline.Handle(request, ct);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDispatchR(typeof(CreateOrderDispatchRHandler).Assembly, withPipelines: true, withNotifications: false);

        services.AddSingleton<FakeOrderRepository>();
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<CreateOrderCommandDispatchR, ValueTask<OrderResult>>), typeof(ValidationStep));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<CreateOrderCommandDispatchR, ValueTask<OrderResult>>), typeof(LoggingStep));
        services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<CreateOrderCommandDispatchR, ValueTask<OrderResult>>), typeof(MetricsStep));

        var provider = services.BuildServiceProvider();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();

        // ── Verification ─────────────────────────────────────────
        ValidationStep.CallCount = 0;
        LoggingStep.CallCount = 0;
        MetricsStep.CallCount = 0;

        var result = _mediator.Send<CreateOrderCommandDispatchR, ValueTask<OrderResult>>(Command, default).GetAwaiter().GetResult();

        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  DISPATCHR REALISTIC PIPELINE VERIFICATION               ║");
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
        _mediator.Send<CreateOrderCommandDispatchR, ValueTask<OrderResult>>(Command, default).GetAwaiter().GetResult();
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
    public async Task<OrderResult> DispatchR_RealisticPipeline()
        => await _mediator.Send<CreateOrderCommandDispatchR, ValueTask<OrderResult>>(Command, default);
}
