// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Unique request types for AOT closure tests ────────────────────

public sealed record AotClosurePing : IRequest<Unit>;
public sealed record AotClosureQuery : IRequest<int>;
public sealed record AotNoBehaviorPing : IRequest<Unit>;
public sealed record AotLifetimePing : IRequest<Unit>;

// ── Handlers ──────────────────────────────────────────────────────

public sealed class AotClosurePingHandler : IRequestHandler<AotClosurePing, Unit>
{
    public ValueTask<Unit> Handle(AotClosurePing request, CancellationToken ct) => new(Unit.Value);
}

public sealed class AotClosureQueryHandler : IRequestHandler<AotClosureQuery, int>
{
    public ValueTask<int> Handle(AotClosureQuery request, CancellationToken ct) => new(42);
}

public sealed class AotNoBehaviorPingHandler : IRequestHandler<AotNoBehaviorPing, Unit>
{
    public ValueTask<Unit> Handle(AotNoBehaviorPing request, CancellationToken ct) => new(Unit.Value);
}

public sealed class AotLifetimePingHandler : IRequestHandler<AotLifetimePing, Unit>
{
    public ValueTask<Unit> Handle(AotLifetimePing request, CancellationToken ct) => new(Unit.Value);
}

// ── Open-generic behaviors for testing ────────────────────────────

public sealed class AotCountBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public AotCountBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add("count:before");
        var result = await next.Handle(request, ct);
        _log.Add("count:after");
        return result;
    }
}

public sealed class AotTimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public AotTimingBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add("timing:before");
        var result = await next.Handle(request, ct);
        _log.Add("timing:after");
        return result;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

/// <summary>
/// Verifies that the source generator's AOT-safe open-generic closure code
/// correctly replaces open-generic pipeline behavior ServiceDescriptors with
/// closed-generic ones so the DI container never calls MakeGenericType
/// (which fails for value-type TResponse under Native AOT).
/// </summary>
public class OpenGenericClosureTests
{
    [Fact]
    public async Task OpenGenericBehavior_ValueTypeResponse_ClosesAndExecutes()
    {
        // Arrange: open-generic behavior with value-type TResponse (Unit)
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotCountBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new AotClosurePing(), TestContext.Current.CancellationToken);

        // Assert: behavior ran, correct result
        result.ShouldBe(Unit.Value);
        log.ShouldBe(new[] { "count:before", "count:after" });
    }

    [Fact]
    public void OpenGenericBehavior_ServiceCollection_ReplacesOpenWithClosedDescriptors()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotCountBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        // Open-generic descriptor should be removed
        var openGeneric = services
            .Where(d => d.ServiceType.IsGenericTypeDefinition &&
                        d.ServiceType == typeof(IPipelineBehavior<,>) &&
                        d.ImplementationType == typeof(AotCountBehavior<,>))
            .ToList();
        openGeneric.ShouldBeEmpty();

        // Closed-generic for Unit (value type) response should exist
        var closedUnit = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<AotClosurePing, Unit>) &&
                        d.ImplementationType == typeof(AotCountBehavior<AotClosurePing, Unit>))
            .ToList();
        closedUnit.Count.ShouldBe(1);
        closedUnit[0].Lifetime.ShouldBe(ServiceLifetime.Transient);

        // Closed-generic for int (value type) response should also exist
        var closedInt = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<AotClosureQuery, int>) &&
                        d.ImplementationType == typeof(AotCountBehavior<AotClosureQuery, int>))
            .ToList();
        closedInt.Count.ShouldBe(1);
    }

    [Fact]
    public void OpenGenericBehavior_Lifetime_IsPreserved()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AotCountBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        var closedUnit = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<AotLifetimePing, Unit>) &&
                        d.ImplementationType == typeof(AotCountBehavior<AotLifetimePing, Unit>))
            .Single();
        closedUnit.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task MultipleBehaviors_OpenGeneric_OrderPreserved()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        // Register in specific order
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotCountBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotTimingBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new AotClosureQuery(), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        // Behaviors execute in registration order (outermost first)
        log.ShouldBe(new[] { "count:before", "timing:before", "timing:after", "count:after" });
    }

    [Fact]
    public void NoBehaviorRegistrations_ClosureDoesNotInterfere()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        // No open-generic behavior registrations
        services.PrecompilePipelines();

        // No closed-generic behavior descriptors should have been added
        var behaviorDescriptors = services
            .Where(d => !d.ServiceType.IsGenericTypeDefinition &&
                        d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>) &&
                        d.ServiceType.GenericTypeArguments.Contains(typeof(AotNoBehaviorPing)))
            .ToList();
        behaviorDescriptors.ShouldBeEmpty();
    }
}
