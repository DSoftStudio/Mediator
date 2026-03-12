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
/// Measures Send() dispatch overhead with 5 pipeline behaviors.
/// Compares:
/// - direct handler call (no pipeline — baseline)
/// - DSoftStudio compile-time dispatch
/// - MediatR runtime dispatch
/// - DispatchR dispatch
/// - martinothamar/Mediator source-generated dispatch
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class SendBenchmarks
{
    private static readonly PingWithPipeline DSoftMessage = new();
    private static readonly PingMediatR MediatRMessage = new();
    private static readonly PingDispatchR DispatchRMessage = new();
    private static readonly PingMediatorSG MediatorSGMessage = new();

    private PingHandler _directHandler = null!;
    private IMediator _mediator = null!;
    private MediatR.IMediator _mediatr = null!;
    private DispatchR.IMediator _dispatchr = null!;
    private global::Mediator.IMediator _mediatorsg = null!;

    private IServiceScope _scope = null!;
    private IServiceScope _mediatrScope = null!;
    private IServiceScope _dispatchrScope = null!;
    private IServiceScope _mediatorsgScope = null!;

    // ── MediatR pass-through behaviors ────────────────────────────

    private sealed class MediatRBehavior1 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct) => next();
    }

    private sealed class MediatRBehavior2 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct) => next();
    }

    private sealed class MediatRBehavior3 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct) => next();
    }

    private sealed class MediatRBehavior4 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct) => next();
    }

    private sealed class MediatRBehavior5 : MediatR.IPipelineBehavior<PingMediatR, int>
    {
        public Task<int> Handle(PingMediatR r, MediatR.RequestHandlerDelegate<int> next, CancellationToken ct) => next();
    }

    // ── DispatchR pass-through behaviors ──────────────────────────

    private sealed class DispatchRBehavior1 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct) => NextPipeline.Handle(request, ct);
    }

    private sealed class DispatchRBehavior2 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct) => NextPipeline.Handle(request, ct);
    }

    private sealed class DispatchRBehavior3 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct) => NextPipeline.Handle(request, ct);
    }

    private sealed class DispatchRBehavior4 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct) => NextPipeline.Handle(request, ct);
    }

    private sealed class DispatchRBehavior5 : global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>
    {
        public required global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>> NextPipeline { get; set; }
        public ValueTask<int> Handle(PingDispatchR request, CancellationToken ct) => NextPipeline.Handle(request, ct);
    }

    // ── martinothamar/Mediator pass-through behaviors ─────────────

    private sealed class MediatorSGBehavior1 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public ValueTask<int> Handle(PingMediatorSG message, global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next, CancellationToken cancellationToken)
            => next(message, cancellationToken);
    }

    private sealed class MediatorSGBehavior2 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public ValueTask<int> Handle(PingMediatorSG message, global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next, CancellationToken cancellationToken)
            => next(message, cancellationToken);
    }

    private sealed class MediatorSGBehavior3 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public ValueTask<int> Handle(PingMediatorSG message, global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next, CancellationToken cancellationToken)
            => next(message, cancellationToken);
    }

    private sealed class MediatorSGBehavior4 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public ValueTask<int> Handle(PingMediatorSG message, global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next, CancellationToken cancellationToken)
            => next(message, cancellationToken);
    }

    private sealed class MediatorSGBehavior5 : global::Mediator.IPipelineBehavior<PingMediatorSG, int>
    {
        public ValueTask<int> Handle(PingMediatorSG message, global::Mediator.MessageHandlerDelegate<PingMediatorSG, int> next, CancellationToken cancellationToken)
            => next(message, cancellationToken);
    }

    [GlobalSetup]
    public void Setup()
    {
        _directHandler = new PingHandler();

        // ── DSoftStudio Mediator (5 behaviors) ────────────────────
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
            _scope = provider.CreateScope();
            _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        }

        // ── MediatR 14.x (5 behaviors) ───────────────────────────
        {
            var services = new ServiceCollection();

            services.AddLogging();

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(PingMediatRHandler).Assembly));

            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(MediatRBehavior1));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(MediatRBehavior2));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(MediatRBehavior3));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(MediatRBehavior4));
            services.AddTransient(typeof(MediatR.IPipelineBehavior<PingMediatR, int>), typeof(MediatRBehavior5));

            var provider = services.BuildServiceProvider();
            _mediatrScope = provider.CreateScope();
            _mediatr = _mediatrScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        }

        // ── DispatchR 2.x (5 behaviors) ──────────────────────────
        {
            var services = new ServiceCollection();

            services.AddDispatchR(typeof(PingDispatchRHandler).Assembly, withPipelines: true, withNotifications: false);

            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(DispatchRBehavior1));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(DispatchRBehavior2));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(DispatchRBehavior3));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(DispatchRBehavior4));
            services.AddScoped(typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<PingDispatchR, ValueTask<int>>), typeof(DispatchRBehavior5));

            var provider = services.BuildServiceProvider();
            _dispatchrScope = provider.CreateScope();
            _dispatchr = _dispatchrScope.ServiceProvider.GetRequiredService<DispatchR.IMediator>();
        }

        // ── martinothamar/Mediator (source-generated, 5 behaviors) ──
        {
            var services = new ServiceCollection();
            MediatorSGHelper.AddMediatorSG(services);

            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(MediatorSGBehavior1));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(MediatorSGBehavior2));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(MediatorSGBehavior3));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(MediatorSGBehavior4));
            services.AddSingleton(typeof(global::Mediator.IPipelineBehavior<PingMediatorSG, int>), typeof(MediatorSGBehavior5));

            var provider = services.BuildServiceProvider();
            _mediatorsgScope = provider.CreateScope();
            _mediatorsg = _mediatorsgScope.ServiceProvider.GetRequiredService<global::Mediator.IMediator>();
        }

        // Warmup all mediators (avoid cold start in benchmarks)
        _directHandler.Handle(new Ping(), default).GetAwaiter().GetResult();
        _mediator.Send<PingWithPipeline, int>(DSoftMessage).GetAwaiter().GetResult();
        _mediatr.Send(MediatRMessage).GetAwaiter().GetResult();
        _dispatchr.Send<PingDispatchR, ValueTask<int>>(DispatchRMessage, default).GetAwaiter().GetResult();
        _mediatorsg.Send(MediatorSGMessage).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope?.Dispose();
        _mediatrScope?.Dispose();
        _dispatchrScope?.Dispose();
        _mediatorsgScope?.Dispose();
    }

    // ── Baseline ─────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public async Task<int> Direct_Send()
        => await _directHandler.Handle(new Ping(), default);

    // ── DSoftStudio Mediator ─────────────────────────────────────

    [Benchmark]
    public async Task<int> DSoft_Send()
        => await _mediator.Send<PingWithPipeline, int>(DSoftMessage);

    // ── MediatR ──────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> MediatR_Send()
        => await _mediatr.Send(MediatRMessage);

    // ── DispatchR ─────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> DispatchR_Send()
        => await _dispatchr.Send<PingDispatchR, ValueTask<int>>(DispatchRMessage, default);

    // ── martinothamar/Mediator (source-generated) ─────────────────

    [Benchmark]
    public async Task<int> MediatorSG_Send()
        => await _mediatorsg.Send(MediatorSGMessage);
}
