// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Request types covering all value-type responses ───────────────

public sealed record AotSafetyUnitCmd : IRequest<Unit>;
public sealed record AotSafetyIntQuery : IRequest<int>;
public sealed record AotSafetyBoolQuery : IRequest<bool>;

// ── Handlers ──────────────────────────────────────────────────────

public sealed class AotSafetyUnitCmdHandler : IRequestHandler<AotSafetyUnitCmd, Unit>
{
    public ValueTask<Unit> Handle(AotSafetyUnitCmd request, CancellationToken ct) => new(Unit.Value);
}

public sealed class AotSafetyIntQueryHandler : IRequestHandler<AotSafetyIntQuery, int>
{
    public ValueTask<int> Handle(AotSafetyIntQuery request, CancellationToken ct) => new(42);
}

public sealed class AotSafetyBoolQueryHandler : IRequestHandler<AotSafetyBoolQuery, bool>
{
    public ValueTask<bool> Handle(AotSafetyBoolQuery request, CancellationToken ct) => new(true);
}

// ── Open-generic behavior for safety tests ────────────────────────

public sealed class AotSafetyLogBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public AotSafetyLogBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        _log.Add($"log:{typeof(TRequest).Name}:before");
        var result = await next.Handle(request, ct);
        _log.Add($"log:{typeof(TRequest).Name}:after");
        return result;
    }
}

public sealed class AotSafetyAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public AotSafetyAuditBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        _log.Add($"audit:{typeof(TRequest).Name}:before");
        var result = await next.Handle(request, ct);
        _log.Add($"audit:{typeof(TRequest).Name}:after");
        return result;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

/// <summary>
/// End-to-end integration tests that mirror the NativeAotVerification console app.
/// These validate that <c>PrecompilePipelines</c> replaces open-generic
/// <c>IPipelineBehavior&lt;,&gt;</c> descriptors with closed-generic versions
/// so that no <c>MakeGenericType</c> call is needed at runtime — which would
/// crash under Native AOT for value-type TResponse.
/// </summary>
public class NativeAotSafetyTests
{
    // ── 1. End-to-end: mirrors NativeAotVerification app ──────────

    [Fact]
    public async Task EndToEnd_OpenGenericBehavior_ReplacedAndResolvable()
    {
        // Arrange — same setup as the verification console app
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotSafetyLogBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        // Assert: no open-generic descriptors remain
        var openGeneric = services
            .Where(d => d.ServiceType.IsGenericTypeDefinition
                     && d.ServiceType == typeof(IPipelineBehavior<,>)
                     && d.ImplementationType == typeof(AotSafetyLogBehavior<,>))
            .ToList();
        openGeneric.ShouldBeEmpty("open-generic descriptors must be removed after PrecompilePipelines");

        // Assert: closed descriptors exist for Unit (struct) and int (value type)
        services.ShouldContain(d =>
            d.ServiceType == typeof(IPipelineBehavior<AotSafetyUnitCmd, Unit>)
            && d.ImplementationType == typeof(AotSafetyLogBehavior<AotSafetyUnitCmd, Unit>));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IPipelineBehavior<AotSafetyIntQuery, int>)
            && d.ImplementationType == typeof(AotSafetyLogBehavior<AotSafetyIntQuery, int>));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IPipelineBehavior<AotSafetyBoolQuery, bool>)
            && d.ImplementationType == typeof(AotSafetyLogBehavior<AotSafetyBoolQuery, bool>));

        // Act & Assert: actual resolution and execution
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var unitResult = await mediator.Send(new AotSafetyUnitCmd(), TestContext.Current.CancellationToken);
        unitResult.ShouldBe(Unit.Value);

        var intResult = await mediator.Send(new AotSafetyIntQuery(), TestContext.Current.CancellationToken);
        intResult.ShouldBe(42);

        var boolResult = await mediator.Send(new AotSafetyBoolQuery(), TestContext.Current.CancellationToken);
        boolResult.ShouldBe(true);

        // Verify every request ran through the behavior
        log.ShouldContain("log:AotSafetyUnitCmd:before");
        log.ShouldContain("log:AotSafetyUnitCmd:after");
        log.ShouldContain("log:AotSafetyIntQuery:before");
        log.ShouldContain("log:AotSafetyIntQuery:after");
        log.ShouldContain("log:AotSafetyBoolQuery:before");
        log.ShouldContain("log:AotSafetyBoolQuery:after");
    }

    // ── 2. Multiple open-generic behaviors ────────────────────────

    [Fact]
    public async Task MultipleBehaviors_AllClosedAndExecuteInOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotSafetyLogBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotSafetyAuditBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        // No open-generic descriptors for either behavior type
        var openGeneric = services
            .Where(d => d.ServiceType.IsGenericTypeDefinition
                     && d.ServiceType == typeof(IPipelineBehavior<,>))
            .ToList();
        openGeneric.ShouldBeEmpty("all open-generic behavior descriptors must be closed");

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new AotSafetyIntQuery(), TestContext.Current.CancellationToken);
        result.ShouldBe(42);

        // Both behaviors ran in registration order (outermost first)
        log.ShouldBe(new[]
        {
            "log:AotSafetyIntQuery:before",
            "audit:AotSafetyIntQuery:before",
            "audit:AotSafetyIntQuery:after",
            "log:AotSafetyIntQuery:after"
        });
    }

    // ── 3. Zero open-generic IPipelineBehavior<,> after precompile ─

    [Fact]
    public void PrecompilePipelines_LeavesZeroOpenGenericBehaviorDescriptors()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotSafetyLogBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotSafetyAuditBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        // Assert: absolutely zero open-generic IPipelineBehavior<,> descriptors
        var allOpenBehaviors = services
            .Where(d => d.ServiceType.IsGenericTypeDefinition
                     && d.ServiceType == typeof(IPipelineBehavior<,>))
            .ToList();

        allOpenBehaviors.ShouldBeEmpty(
            "PrecompilePipelines must replace ALL open-generic IPipelineBehavior<,> " +
            "descriptors with closed-generic equivalents for Native AOT safety");
    }

    // ── 4. Closed descriptors preserve original lifetime ──────────

    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void ClosedDescriptors_PreserveLifetime(ServiceLifetime lifetime)
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.Add(new ServiceDescriptor(
            typeof(IPipelineBehavior<,>),
            typeof(AotSafetyLogBehavior<,>),
            lifetime));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        var closed = services.Single(d =>
            d.ServiceType == typeof(IPipelineBehavior<AotSafetyUnitCmd, Unit>)
            && d.ImplementationType == typeof(AotSafetyLogBehavior<AotSafetyUnitCmd, Unit>));

        closed.Lifetime.ShouldBe(lifetime);
    }

    // ── 5. No duplicate closed descriptors ────────────────────────

    [Fact]
    public void PrecompilePipelines_DoesNotCreateDuplicateClosedDescriptors()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotSafetyLogBehavior<,>));
        services.AddSingleton(log);

        // Call PrecompilePipelines twice
        services.PrecompilePipelines();
        services.PrecompilePipelines();

        var closedUnit = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<AotSafetyUnitCmd, Unit>)
                     && d.ImplementationType == typeof(AotSafetyLogBehavior<AotSafetyUnitCmd, Unit>))
            .ToList();

        closedUnit.Count.ShouldBe(1, "calling PrecompilePipelines twice must not duplicate descriptors");
    }
}
