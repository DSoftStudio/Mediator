// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Dedicated message type to avoid static state collisions ───────

public record HijackPing : IRequest<int>;

public sealed class HijackPingHandler : IRequestHandler<HijackPing, int>
{
    public ValueTask<int> Handle(HijackPing request, CancellationToken cancellationToken)
        => new(42);
}

// ── Logging behavior (records order) ──────────────────────────────

public sealed class LoggingBehavior : IPipelineBehavior<HijackPing, int>
{
    private readonly List<string> _log;

    public LoggingBehavior(List<string> log) => _log = log;

    public async ValueTask<int> Handle(
        HijackPing request,
        IRequestHandler<HijackPing, int> next,
        CancellationToken cancellationToken)
    {
        _log.Add("Logging:before");
        var result = await next.Handle(request, cancellationToken);
        _log.Add("Logging:after");
        return result;
    }
}

// ── Validation behavior (records order) ───────────────────────────

public sealed class ValidationBehavior : IPipelineBehavior<HijackPing, int>
{
    private readonly List<string> _log;

    public ValidationBehavior(List<string> log) => _log = log;

    public async ValueTask<int> Handle(
        HijackPing request,
        IRequestHandler<HijackPing, int> next,
        CancellationToken cancellationToken)
    {
        _log.Add("Validation:before");
        var result = await next.Handle(request, cancellationToken);
        _log.Add("Validation:after");
        return result;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

public class PipelineHijackingTests
{
    [Fact]
    public async Task Pipeline_ExecutesInCorrectOrder_BehaviorsThenHandler()
    {
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();
        services.AddSingleton(log);

        // Register behaviors in order: Logging → Validation
        services.AddTransient<IPipelineBehavior<HijackPing, int>>(
            sp => new LoggingBehavior(sp.GetRequiredService<List<string>>()));
        services.AddTransient<IPipelineBehavior<HijackPing, int>>(
            sp => new ValidationBehavior(sp.GetRequiredService<List<string>>()));

        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new HijackPing());

        result.ShouldBe(42);

        // Logging (first registered) wraps Validation (second registered) wraps Handler
        log.ShouldBe(new[] {
            "Logging:before",
            "Validation:before",
            "Validation:after",
            "Logging:after" });
    }

    [Fact]
    public async Task Pipeline_HandlerExecutedExactlyOnce()
    {
        var handlerCallCount = 0;
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();

        services.AddTransient<IRequestHandler<HijackPing, int>>(sp =>
        {
            return new CountingHijackPingHandler(() => Interlocked.Increment(ref handlerCallCount));
        });

        services.AddTransient<IPipelineBehavior<HijackPing, int>>(
            sp => new PassThroughBehavior<HijackPing, int>());
        services.AddTransient<IPipelineBehavior<HijackPing, int>>(
            sp => new PassThroughBehavior<HijackPing, int>());

        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new HijackPing());

        handlerCallCount.ShouldBe(1);
    }
}

// ── Helper handler that counts invocations ────────────────────────

file sealed class CountingHijackPingHandler : IRequestHandler<HijackPing, int>
{
    private readonly Action _onHandle;

    public CountingHijackPingHandler(Action onHandle) => _onHandle = onHandle;

    public ValueTask<int> Handle(HijackPing request, CancellationToken cancellationToken)
    {
        _onHandle();
        return new(42);
    }
}
