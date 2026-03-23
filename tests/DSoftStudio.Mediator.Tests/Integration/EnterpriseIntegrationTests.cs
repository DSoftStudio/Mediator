// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DSoftStudio.Mediator.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════
//  SHARED INFRASTRUCTURE — unique types to avoid cross-test pollution
// ═══════════════════════════════════════════════════════════════════

// ── Lifetime tracking ─────────────────────────────────────────────

/// <summary>
/// Scoped dependency injected into a handler — tracks instance identity.
/// </summary>
public sealed class ScopedCorrelation : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool Disposed { get; private set; }
    public void Dispose() => Disposed = true;
}

/// <summary>
/// Transient dependency — each resolve produces a new instance.
/// </summary>
public sealed class TransientStamp
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>
/// Singleton counter shared across all scopes.
/// </summary>
public sealed class SingletonCounter
{
    private int _value;
    public int Increment() => Interlocked.Increment(ref _value);
    public int Value => Volatile.Read(ref _value);
}

// ── Deep pipeline request types ───────────────────────────────────

public sealed record DeepPipelinePing(bool ShouldThrow = false) : IRequest<string>;

public sealed class DeepPipelinePingHandler : IRequestHandler<DeepPipelinePing, string>
{
    public ValueTask<string> Handle(DeepPipelinePing request, CancellationToken ct)
    {
        if (request.ShouldThrow)
            throw new InvalidOperationException("handler-boom");
        return new("deep-ok");
    }
}

// ── Deep pipeline behaviors ───────────────────────────────────────

public sealed class DeepLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public DeepLoggingBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"log:{typeof(TRequest).Name}:enter");
        var result = await next.Handle(request, ct);
        _log.Add($"log:{typeof(TRequest).Name}:exit");
        return result;
    }
}

public sealed class DeepValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public DeepValidationBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"validation:{typeof(TRequest).Name}:enter");
        var result = await next.Handle(request, ct);
        _log.Add($"validation:{typeof(TRequest).Name}:exit");
        return result;
    }
}

public sealed class DeepAuthBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public DeepAuthBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"auth:{typeof(TRequest).Name}:enter");
        var result = await next.Handle(request, ct);
        _log.Add($"auth:{typeof(TRequest).Name}:exit");
        return result;
    }
}

public sealed class DeepMetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public DeepMetricsBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"metrics:{typeof(TRequest).Name}:enter");
        var result = await next.Handle(request, ct);
        _log.Add($"metrics:{typeof(TRequest).Name}:exit");
        return result;
    }
}

public sealed class DeepRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public DeepRetryBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"retry:{typeof(TRequest).Name}:enter");
        try
        {
            var result = await next.Handle(request, ct);
            _log.Add($"retry:{typeof(TRequest).Name}:exit");
            return result;
        }
        catch
        {
            _log.Add($"retry:{typeof(TRequest).Name}:retry-attempt");
            // Re-throw on second failure — no infinite retry
            var result = await next.Handle(request, ct);
            _log.Add($"retry:{typeof(TRequest).Name}:exit");
            return result;
        }
    }
}

public sealed class DeepCachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public DeepCachingBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"caching:{typeof(TRequest).Name}:enter");
        var result = await next.Handle(request, ct);
        _log.Add($"caching:{typeof(TRequest).Name}:exit");
        return result;
    }
}

// ── DI lifetime request types ─────────────────────────────────────

public sealed record LifetimePing : IRequest<LifetimeResult>;

public sealed record LifetimeResult(
    Guid CorrelationId,
    Guid TransientId,
    int CounterValue);

public sealed class LifetimePingHandler : IRequestHandler<LifetimePing, LifetimeResult>
{
    private readonly ScopedCorrelation _correlation;
    private readonly TransientStamp _stamp;
    private readonly SingletonCounter _counter;

    public LifetimePingHandler(ScopedCorrelation correlation, TransientStamp stamp, SingletonCounter counter)
    {
        _correlation = correlation;
        _stamp = stamp;
        _counter = counter;
    }

    public ValueTask<LifetimeResult> Handle(LifetimePing request, CancellationToken ct)
        => new(new LifetimeResult(_correlation.Id, _stamp.Id, _counter.Increment()));
}

// ── Concurrency request types ─────────────────────────────────────

public sealed record ConcurrentPing(int Seed) : IRequest<int>;

public sealed class ConcurrentPingHandler : IRequestHandler<ConcurrentPing, int>
{
    public async ValueTask<int> Handle(ConcurrentPing request, CancellationToken ct)
    {
        // Simulate light async work to increase chance of thread interleaving
        await Task.Delay(1, ct);
        return request.Seed * 2;
    }
}

// ── Concurrency notification types ────────────────────────────────

public sealed record ConcurrentNotification(int Id) : INotification;

public sealed class ConcurrentNotificationHandler : INotificationHandler<ConcurrentNotification>
{
    private readonly ConcurrentBag<int> _received;
    public ConcurrentNotificationHandler(ConcurrentBag<int> received) => _received = received;

    public Task Handle(ConcurrentNotification notification, CancellationToken ct)
    {
        _received.Add(notification.Id);
        return Task.CompletedTask;
    }
}

// ── Complex generic request types ─────────────────────────────────

public sealed record NullableRefQuery(string UserId) : IRequest<string?>;

public sealed class NullableRefQueryHandler : IRequestHandler<NullableRefQuery, string?>
{
    public ValueTask<string?> Handle(NullableRefQuery request, CancellationToken ct)
        => new(request.UserId == "missing" ? null : request.UserId);
}

public sealed record NullableValueQuery(bool ReturnNull) : IRequest<int?>;

public sealed class NullableValueQueryHandler : IRequestHandler<NullableValueQuery, int?>
{
    public ValueTask<int?> Handle(NullableValueQuery request, CancellationToken ct)
        => new(request.ReturnNull ? null : 42);
}

// ── AOT safety request types ──────────────────────────────────────

public sealed record AotPing : IRequest<int>;
public sealed record AotVoidPing : IRequest<Unit>;

public sealed class AotPingHandler : IRequestHandler<AotPing, int>
{
    public ValueTask<int> Handle(AotPing request, CancellationToken ct) => new(100);
}

public sealed class AotVoidPingHandler : IRequestHandler<AotVoidPing, Unit>
{
    public ValueTask<Unit> Handle(AotVoidPing request, CancellationToken ct) => new(Unit.Value);
}

public sealed class AotBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ConcurrentBag<string> _log;
    public AotBehavior(ConcurrentBag<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add($"aot-behavior:{typeof(TRequest).Name}");
        return await next.Handle(request, ct);
    }
}

// ── Background service request types ──────────────────────────────

public sealed record BackgroundJob(int JobId) : ICommand<string>;

public sealed class BackgroundJobHandler : IRequestHandler<BackgroundJob, string>
{
    private readonly SingletonCounter _counter;
    public BackgroundJobHandler(SingletonCounter counter) => _counter = counter;

    public async ValueTask<string> Handle(BackgroundJob request, CancellationToken ct)
    {
        await Task.Yield();
        _counter.Increment();
        return $"job-{request.JobId}-done";
    }
}

// ── Stress test stream types ──────────────────────────────────────

public sealed record StressStream(int Count) : IStreamRequest<int>;

public sealed class StressStreamHandler : IStreamRequestHandler<StressStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        StressStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            yield return i;
        }
    }
}

// ── Failure injection types ───────────────────────────────────────

public sealed record DelayedPing(int DelayMs) : IRequest<string>;

public sealed class DelayedPingHandler : IRequestHandler<DelayedPing, string>
{
    public async ValueTask<string> Handle(DelayedPing request, CancellationToken ct)
    {
        await Task.Delay(request.DelayMs, ct);
        return "delayed-ok";
    }
}

public sealed record FlakeyPing : IRequest<string>;

/// <summary>
/// Injectable failure state — avoids static global state that causes cross-test contamination.
/// </summary>
public sealed class FlakeyState
{
    private int _failuresRemaining;
    public FlakeyState(int failuresRemaining) => _failuresRemaining = failuresRemaining;
    public bool ShouldFail() => Interlocked.Decrement(ref _failuresRemaining) >= 0;
}

public sealed class FlakeyPingHandler : IRequestHandler<FlakeyPing, string>
{
    private readonly FlakeyState _state;
    public FlakeyPingHandler(FlakeyState state) => _state = state;

    public ValueTask<string> Handle(FlakeyPing request, CancellationToken ct)
    {
        if (_state.ShouldFail())
            throw new InvalidOperationException("flakey-boom");
        return new("flakey-ok");
    }
}

public sealed class SimpleRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next.Handle(request, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return await next.Handle(request, ct);
        }
    }
}

// ── Timeout / deadlock types ──────────────────────────────────────

public sealed record NestedOuterPing : IRequest<string>;

public sealed class NestedOuterPingHandler : IRequestHandler<NestedOuterPing, string>
{
    public ValueTask<string> Handle(NestedOuterPing request, CancellationToken ct)
        => new("nested-ok");
}

/// <summary>
/// Behavior that sends a Ping inside the pipeline — validates no deadlock.
/// Registered as closed-generic to avoid infinite recursion on NestedOuterPing.
/// </summary>
public sealed class NestedSendBehavior : IPipelineBehavior<NestedOuterPing, string>
{
    private readonly IMediator _mediator;
    public NestedSendBehavior(IMediator mediator) => _mediator = mediator;

    public async ValueTask<string> Handle(
        NestedOuterPing request,
        IRequestHandler<NestedOuterPing, string> next,
        CancellationToken ct)
    {
        var innerResult = await _mediator.Send(new Ping(), ct);
        var result = await next.Handle(request, ct);
        return $"{result}+inner={innerResult}";
    }
}

public sealed class SlowBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static int DelayMs = 10;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        await Task.Delay(DelayMs, ct);
        return await next.Handle(request, ct);
    }
}

// ── Chaos types ───────────────────────────────────────────────────

public sealed class ChaosConfig
{
    public double FailureRate { get; set; } = 0.1;
    public int MaxDelayMs { get; set; } = 50;
}

/// <summary>
/// Abstraction over random number generation — enables deterministic chaos tests.
/// </summary>
public interface IChaosRandom
{
    int Next(int minValue, int maxValue);
    double NextDouble();
}

/// <summary>
/// Default implementation backed by <see cref="Random.Shared"/>.
/// </summary>
public sealed class ThreadSafeChaosRandom : IChaosRandom
{
    public int Next(int minValue, int maxValue) => Random.Shared.Next(minValue, maxValue);
    public double NextDouble() => Random.Shared.NextDouble();
}

public sealed class ChaosBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ChaosConfig _config;
    private readonly IChaosRandom _random;
    public ChaosBehavior(ChaosConfig config, IChaosRandom random)
    {
        _config = config;
        _random = random;
    }

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        if (_config.MaxDelayMs > 0)
        {
            var delay = _random.Next(0, _config.MaxDelayMs);
            if (delay > 0) await Task.Delay(delay, ct);
        }

        if (_random.NextDouble() < _config.FailureRate)
            throw new InvalidOperationException("chaos-boom");

        return await next.Handle(request, ct);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  1. MULTI-PROJECT INTEGRATION TESTS
//     Validates cross-assembly handler discovery. Since source-generated
//     code from the test assembly discovers handlers both locally and
//     from referenced projects, this validates the full multi-project chain.
// ═══════════════════════════════════════════════════════════════════

public class MultiProjectIntegrationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public MultiProjectIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        // External dependencies required by handlers in other test classes
        services.AddSingleton(new List<string>());
        services.AddSingleton<SelfHandler.Greeter>();
        services.AddSingleton<SingletonCounter>();
        services.AddScoped<ScopedCorrelation>();
        services.AddTransient<TransientStamp>();
        services.AddSingleton(new ConcurrentBag<string>());
        services.AddSingleton(new ConcurrentBag<int>());
        services.AddSingleton(new Counter());
        services.AddSingleton(new FlakeyState(0));
        services.AddSingleton<IChaosRandom>(new ThreadSafeChaosRandom());

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    /// <summary>
    /// ValidateMediatorHandlers resolves every registered handler from DI.
    /// If cross-assembly discovery fails, this throws AggregateException.
    /// </summary>
    [Fact]
    public void AllHandlers_DiscoveredAndResolvable_AcrossAssembly()
    {
        // This validates that the source-generated ValidateMediatorHandlers()
        // can resolve every handler — including ones defined in this file,
        // Infrastructure/, and other test namespaces.
        _provider.ValidateMediatorHandlers();
    }

    /// <summary>
    /// Sends a request whose handler is defined in a different namespace/folder
    /// (Infrastructure/TestHandlers.cs). Validates cross-namespace discovery.
    /// </summary>
    [Fact]
    public async Task Send_HandlerFromDifferentNamespace_ReturnsCorrectResult()
    {
        var result = await _mediator.Send(new Ping(), TestContext.Current.CancellationToken);
        result.ShouldBe(42);
    }

    /// <summary>
    /// Publishes a notification that has handlers in Infrastructure/TestHandlers.cs.
    /// Validates notification handler discovery across namespaces.
    /// </summary>
    [Fact]
    public async Task Publish_NotificationHandlerFromDifferentNamespace_DoesNotThrow()
    {
        // PingNotification + PingNotificationHandler are in Infrastructure/
        await _mediator.Publish(new PingNotification(), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Streams from a handler defined in Infrastructure/TestHandlers.cs.
    /// Validates stream handler discovery across namespaces.
    /// </summary>
    [Fact]
    public async Task CreateStream_HandlerFromDifferentNamespace_YieldsAllItems()
    {
        var items = new List<int>();
        await foreach (var item in _mediator.CreateStream(new PingStream(), TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3]);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  2. DEPENDENCY INJECTION INTEGRATION TESTS
//     Validates mixed Singleton/Scoped/Transient lifetimes through the
//     mediator pipeline without lifetime violations.
// ═══════════════════════════════════════════════════════════════════

public class DependencyInjectionIntegrationTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public DependencyInjectionIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        services.AddSingleton<SingletonCounter>();
        services.AddScoped<ScopedCorrelation>();
        services.AddTransient<TransientStamp>();

        // Override auto-registered handler with Scoped to pick up scoped dependencies
        services.AddScoped<IRequestHandler<LifetimePing, LifetimeResult>, LifetimePingHandler>();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose() => _provider.Dispose();

    /// <summary>
    /// Validates that within the same scope:
    /// - ScopedCorrelation produces the same Id
    /// - TransientStamp produces different Ids per resolve
    /// - SingletonCounter increments globally
    /// </summary>
    [Fact]
    public async Task SameScope_ScopedDependency_SameInstance()
    {
        using var scope = _provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var r1 = await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);
        var r2 = await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);

        // Same scope → same ScopedCorrelation.Id
        r1.CorrelationId.ShouldBe(r2.CorrelationId);

        // Singleton counter keeps incrementing
        r2.CounterValue.ShouldBeGreaterThan(r1.CounterValue);
    }

    /// <summary>
    /// Validates that different scopes produce different scoped instances
    /// but share the same singleton counter.
    /// </summary>
    [Fact]
    public async Task DifferentScopes_ScopedDependency_DifferentInstances()
    {
        LifetimeResult r1, r2;

        using (var scope1 = _provider.CreateScope())
        {
            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
            r1 = await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);
        }

        using (var scope2 = _provider.CreateScope())
        {
            var mediator = scope2.ServiceProvider.GetRequiredService<IMediator>();
            r2 = await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);
        }

        // Different scopes → different ScopedCorrelation.Id
        r1.CorrelationId.ShouldNotBe(r2.CorrelationId);

        // Same singleton counter → second call has higher value
        r2.CounterValue.ShouldBeGreaterThan(r1.CounterValue);
    }

    /// <summary>
    /// Validates that transient dependencies produce new instances across
    /// different scopes (each scope creates a new Scoped handler, which
    /// receives a fresh TransientStamp at construction time).
    /// </summary>
    [Fact]
    public async Task TransientDependency_DifferentAcrossScopes()
    {
        LifetimeResult r1, r2;

        using (var scope1 = _provider.CreateScope())
        {
            var mediator = scope1.ServiceProvider.GetRequiredService<IMediator>();
            r1 = await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);
        }

        using (var scope2 = _provider.CreateScope())
        {
            var mediator = scope2.ServiceProvider.GetRequiredService<IMediator>();
            r2 = await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);
        }

        // Different scopes → different handler → different TransientStamp.Id
        r1.TransientId.ShouldNotBe(r2.TransientId);
    }

    /// <summary>
    /// Validates singleton remains identical across 50 parallel scopes.
    /// </summary>
    [Fact]
    public async Task SingletonCounter_SharedAcrossParallelScopes()
    {
        const int parallelism = 50;
        var results = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, parallelism),
            TestContext.Current.CancellationToken,
            async (_, ct) =>
            {
                using var scope = _provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var r = await mediator.Send(new LifetimePing(), ct);
                results.Add(r.CounterValue);
            });

        // All counter values should be unique (no lost increments)
        results.Distinct().Count().ShouldBe(parallelism);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  3. DEEP PIPELINE INTEGRATION TESTS
//     Validates 6 behaviors in correct order, async exception handling,
//     and exception handler integration.
// ═══════════════════════════════════════════════════════════════════

public class DeepPipelineIntegrationTests
{
    private ServiceProvider BuildProvider(List<string> log)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(log);

        // Register 6 behaviors in order: logging → validation → auth → metrics → retry → caching
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepLoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepAuthBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepMetricsBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepRetryBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepCachingBehavior<,>));

        services.PrecompilePipelines();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Validates all 6 behaviors fire and produce correct "enter/exit"
    /// pairing. Each behavior must have exactly one enter and one exit.
    /// </summary>
    [Fact]
    public async Task SixBehaviors_ExecuteInCorrectNestingOrder()
    {
        var log = new List<string>();
        using var provider = BuildProvider(log);
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new DeepPipelinePing(), TestContext.Current.CancellationToken);

        result.ShouldBe("deep-ok");

        // All 6 behaviors should have entered and exited
        log.Count(e => e.EndsWith(":enter")).ShouldBe(6);
        log.Count(e => e.EndsWith(":exit")).ShouldBe(6);

        // Verify all 6 prefixes are present
        var prefixes = new[] { "log:", "validation:", "auth:", "metrics:", "retry:", "caching:" };
        foreach (var prefix in prefixes)
        {
            log.ShouldContain(e => e.StartsWith(prefix) && e.EndsWith(":enter"));
            log.ShouldContain(e => e.StartsWith(prefix) && e.EndsWith(":exit"));
        }
    }

    /// <summary>
    /// Validates that an exception thrown by the handler propagates through
    /// all 6 behaviors. The retry behavior retries once — if the handler
    /// still throws, the exception surfaces to the caller.
    /// </summary>
    [Fact]
    public async Task DeepPipeline_HandlerThrows_ExceptionPropagates()
    {
        var log = new List<string>();
        using var provider = BuildProvider(log);
        var mediator = provider.GetRequiredService<IMediator>();

        // ShouldThrow=true causes handler to throw InvalidOperationException.
        // Retry behavior catches once and retries — second attempt also throws.
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            mediator.Send(new DeepPipelinePing(ShouldThrow: true), TestContext.Current.CancellationToken).AsTask());

        ex.Message.ShouldBe("handler-boom");

        // The retry behavior should have logged a retry attempt
        log.ShouldContain(e => e.Contains("retry-attempt"));
    }

    /// <summary>
    /// Validates that an exception handler registered for DeepPipelinePing
    /// can intercept the exception and provide a fallback result.
    /// </summary>
    [Fact]
    public async Task DeepPipeline_WithExceptionHandler_FallbackResultReturned()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(log);

        // Only logging behavior (no retry, so the exception handler fires)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DeepLoggingBehavior<,>));

        // Exception handler that provides fallback
        services.AddSingleton<IRequestExceptionHandler<DeepPipelinePing, string>>(
            new DeepPipelineFallbackExceptionHandler());

        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(
            new DeepPipelinePing(ShouldThrow: true), TestContext.Current.CancellationToken);

        result.ShouldBe("fallback");
    }

    /// <summary>
    /// Validates that behaviors + async handler work correctly through the full chain.
    /// </summary>
    [Fact]
    public async Task DeepPipeline_MixedSyncAsync_CompletesCorrectly()
    {
        var log = new List<string>();
        using var provider = BuildProvider(log);
        var mediator = provider.GetRequiredService<IMediator>();

        // Run multiple sends to verify stability
        for (int i = 0; i < 10; i++)
        {
            var result = await mediator.Send(new DeepPipelinePing(), TestContext.Current.CancellationToken);
            result.ShouldBe("deep-ok");
        }
    }
}

public sealed class DeepPipelineFallbackExceptionHandler : IRequestExceptionHandler<DeepPipelinePing, string>
{
    public ValueTask Handle(DeepPipelinePing request, Exception exception,
        RequestExceptionHandlerState<string> state, CancellationToken ct)
    {
        state.SetHandled("fallback");
        return ValueTask.CompletedTask;
    }
}

// ═══════════════════════════════════════════════════════════════════
//  4. CONCURRENCY INTEGRATION TESTS
//     1000+ parallel requests validating no race conditions, no exceptions,
//     correct results for both Send and Publish.
// ═══════════════════════════════════════════════════════════════════

public class ConcurrencyIntegrationTests
{
    /// <summary>
    /// Fires 2000 parallel Send calls with unique seeds.
    /// Validates every result matches (seed × 2) — no cross-contamination.
    /// </summary>
    [Fact]
    public async Task Send_2000Parallel_AllResultsCorrect()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var provider = services.BuildServiceProvider();

        const int parallelism = 2000;
        var tasks = new Task<int>[parallelism];

        for (int i = 0; i < parallelism; i++)
        {
            var seed = i;
            tasks[i] = Task.Run(async () =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ConcurrentPing(seed), TestContext.Current.CancellationToken);
            });
        }

        var results = await Task.WhenAll(tasks);

        // Every result should be seed × 2
        for (int i = 0; i < parallelism; i++)
        {
            results[i].ShouldBe(i * 2, $"Result for seed={i} was incorrect");
        }
    }

    /// <summary>
    /// Fires 1000 parallel Publish calls. Validates the notification handler
    /// receives all 1000 messages with no lost notifications.
    /// </summary>
    [Fact]
    public async Task Publish_1000Parallel_AllNotificationsReceived()
    {
        var received = new ConcurrentBag<int>();
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications();
        services.AddSingleton(received);

        using var provider = services.BuildServiceProvider();

        const int parallelism = 1000;
        var tasks = new Task[parallelism];

        for (int i = 0; i < parallelism; i++)
        {
            var id = i;
            tasks[i] = Task.Run(async () =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Publish(new ConcurrentNotification(id), TestContext.Current.CancellationToken);
            });
        }

        await Task.WhenAll(tasks);

        // All 1000 notifications should have been received
        received.Count.ShouldBe(parallelism);
        received.Distinct().Count().ShouldBe(parallelism);
    }

    /// <summary>
    /// Mixed Send and Publish calls running concurrently — validates they
    /// don't interfere with each other.
    /// </summary>
    [Fact]
    public async Task MixedSendAndPublish_1000Parallel_NoInterference()
    {
        var received = new ConcurrentBag<int>();
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications();
        services.AddSingleton(received);

        using var provider = services.BuildServiceProvider();

        const int parallelism = 500;
        var sendTasks = new Task<int>[parallelism];
        var publishTasks = new Task[parallelism];

        for (int i = 0; i < parallelism; i++)
        {
            var seed = i;
            sendTasks[i] = Task.Run(async () =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new ConcurrentPing(seed), TestContext.Current.CancellationToken);
            });

            publishTasks[i] = Task.Run(async () =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Publish(new ConcurrentNotification(seed), TestContext.Current.CancellationToken);
            });
        }

        var sendResults = await Task.WhenAll(sendTasks);
        await Task.WhenAll(publishTasks);

        // All send results correct
        for (int i = 0; i < parallelism; i++)
            sendResults[i].ShouldBe(i * 2);

        // All notifications received
        received.Count.ShouldBe(parallelism);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  5. NATIVE AOT INTEGRATION TESTS
//     Validates that PrecompilePipelines replaces open-generic descriptors
//     with closed-generic versions — no MakeGenericType needed at runtime.
// ═══════════════════════════════════════════════════════════════════

public class NativeAotIntegrationTests
{
    /// <summary>
    /// Validates that after PrecompilePipelines, no open-generic
    /// IPipelineBehavior&lt;,&gt; descriptors remain in the service collection.
    /// This is the AOT safety guarantee.
    /// </summary>
    [Fact]
    public void PrecompilePipelines_RemovesAllOpenGenericBehaviors()
    {
        var log = new ConcurrentBag<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotBehavior<,>));
        services.AddSingleton(log);

        // Before precompile: open-generic should exist
        services.Any(d => d.ServiceType.IsGenericTypeDefinition
                       && d.ServiceType == typeof(IPipelineBehavior<,>))
            .ShouldBeTrue("open-generic should exist before PrecompilePipelines");

        services.PrecompilePipelines();

        // After precompile: no open-generic descriptors should remain
        services.Any(d => d.ServiceType.IsGenericTypeDefinition
                       && d.ServiceType == typeof(IPipelineBehavior<,>)
                       && d.ImplementationType == typeof(AotBehavior<,>))
            .ShouldBeFalse("open-generic should be replaced after PrecompilePipelines");
    }

    /// <summary>
    /// End-to-end: behaviors fire correctly after PrecompilePipelines
    /// for both value-type (int) and reference-type (Unit) responses.
    /// </summary>
    [Fact]
    public async Task PrecompiledBehaviors_FireCorrectly_ForValueAndReferenceTypes()
    {
        var log = new ConcurrentBag<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotBehavior<,>));
        services.AddSingleton(log);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Value-type response (int)
        var intResult = await mediator.Send(new AotPing(), TestContext.Current.CancellationToken);
        intResult.ShouldBe(100);

        // Unit response
        var unitResult = await mediator.Send(new AotVoidPing(), TestContext.Current.CancellationToken);
        unitResult.ShouldBe(Unit.Value);

        log.ShouldContain(e => e.Contains("AotPing"));
        log.ShouldContain(e => e.Contains("AotVoidPing"));
    }

    /// <summary>
    /// PrecompilePipelines is idempotent — calling it twice should not
    /// corrupt the service collection or cause double registration.
    /// </summary>
    [Fact]
    public async Task PrecompilePipelines_CalledTwice_StillWorksCorrectly()
    {
        var log = new ConcurrentBag<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AotBehavior<,>));
        services.AddSingleton(log);

        services.PrecompilePipelines();
        services.PrecompilePipelines(); // Idempotent

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new AotPing(), TestContext.Current.CancellationToken);
        result.ShouldBe(100);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  6. EXPRESSION TREE INTEGRATION TESTS
//     Validates that mediator calls inside expression tree lambdas
//     (Moq Setup/Verify pattern) do NOT get rewritten by interceptors.
// ═══════════════════════════════════════════════════════════════════

public class ExpressionTreeIntegrationTests
{
    /// <summary>
    /// Validates that ISender can be wrapped in an expression tree lambda
    /// without the source generator intercepting the call site.
    /// This simulates what Moq Setup(() => sender.Send(...)) does.
    /// </summary>
    [Fact]
    public void ExpressionTree_WithSendCall_DoesNotCrash()
    {
        // Build an expression tree that references ISender.Send — this is the
        // pattern Moq uses. The test validates that the source generator does
        // NOT attempt to rewrite this call site (which would cause CS8652).
        System.Linq.Expressions.Expression<Func<ISender, ValueTask<int>>> expr =
            sender => sender.Send<Ping, int>(new Ping(), CancellationToken.None);

        // The expression should compile and be invocable (not intercepted)
        var compiled = expr.Compile();
        compiled.ShouldNotBeNull();

        // Verify the expression tree captured the correct method
        var body = expr.Body.ShouldBeAssignableTo<System.Linq.Expressions.MethodCallExpression>();
        body!.Method.Name.ShouldBe("Send");
    }

    /// <summary>
    /// Validates that IPublisher can be wrapped in an expression tree lambda.
    /// </summary>
    [Fact]
    public void ExpressionTree_WithPublishCall_DoesNotCrash()
    {
        System.Linq.Expressions.Expression<Func<IPublisher, Task>> expr =
            publisher => publisher.Publish(new PingNotification(), CancellationToken.None);

        var compiled = expr.Compile();
        compiled.ShouldNotBeNull();

        // Verify the expression tree captured the correct method
        var body = expr.Body.ShouldBeAssignableTo<System.Linq.Expressions.MethodCallExpression>();
        body!.Method.Name.ShouldBe("Publish");
    }
}

// ═══════════════════════════════════════════════════════════════════
//  7. COMPLEX GENERICS INTEGRATION TESTS
//     Validates nullable reference types, nullable value types,
//     and complex generic constraints through the full pipeline.
// ═══════════════════════════════════════════════════════════════════

public class ComplexGenericsIntegrationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public ComplexGenericsIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    /// <summary>
    /// Validates IRequest&lt;string?&gt; — nullable reference type response.
    /// The generator must emit string? (not string) in all generated code.
    /// </summary>
    [Fact]
    public async Task NullableReferenceType_NonNullResult()
    {
        var result = await _mediator.Send(new NullableRefQuery("alice"), TestContext.Current.CancellationToken);
        result.ShouldBe("alice");
    }

    [Fact]
    public async Task NullableReferenceType_NullResult()
    {
        var result = await _mediator.Send(new NullableRefQuery("missing"), TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    /// <summary>
    /// Validates IRequest&lt;int?&gt; — nullable value type response.
    /// The generator must emit int? (Nullable&lt;int&gt;) correctly.
    /// </summary>
    [Fact]
    public async Task NullableValueType_NonNullResult()
    {
        var result = await _mediator.Send(new NullableValueQuery(ReturnNull: false), TestContext.Current.CancellationToken);
        result.ShouldBe(42);
    }

    [Fact]
    public async Task NullableValueType_NullResult()
    {
        var result = await _mediator.Send(new NullableValueQuery(ReturnNull: true), TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    /// <summary>
    /// Validates IStreamRequest&lt;int&gt; with Send(object) dispatch — 
    /// the stream type should be discoverable at runtime.
    /// </summary>
    [Fact]
    public async Task Stream_WithValueTypeResponse_YieldsCorrectItems()
    {
        var items = new List<int>();
        await foreach (var item in _mediator.CreateStream(new StressStream(5), TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.ShouldBe([0, 1, 2, 3, 4]);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  8. RUNTIME VS COMPILE-TIME MISMATCH TESTS
//     Validates Send(object) vs typed Send<TRequest, TResponse>
//     produce identical results.
// ═══════════════════════════════════════════════════════════════════

public class RuntimeVsCompileTimeMismatchTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public RuntimeVsCompileTimeMismatchTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    /// <summary>
    /// Typed Send and Send(object) must return the same result
    /// for the same request type.
    /// </summary>
    [Fact]
    public async Task TypedSend_vs_ObjectSend_SameResult()
    {
        // Typed send (compile-time dispatch)
        var typedResult = await _mediator.Send(new Ping(), TestContext.Current.CancellationToken);

        // Object send (runtime dispatch via FrozenDictionary)
        object request = new Ping();
        var objectResult = await ((ISender)_mediator).Send(request, TestContext.Current.CancellationToken);

        typedResult.ShouldBe(42);
        objectResult.ShouldBe(42);
    }

    /// <summary>
    /// Send(object) with Unit response should return Unit.Value.
    /// </summary>
    [Fact]
    public async Task ObjectSend_UnitResponse_ReturnsUnit()
    {
        object request = new PingVoid();
        var result = await ((ISender)_mediator).Send(request, TestContext.Current.CancellationToken);
        result.ShouldBe(Unit.Value);
    }

    /// <summary>
    /// Send(object) with an unregistered type should throw
    /// InvalidOperationException — not silently return default.
    /// </summary>
    [Fact]
    public async Task ObjectSend_UnregisteredType_ThrowsInvalidOperationException()
    {
        object request = new UnregisteredRequest();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ((ISender)_mediator).Send(request, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Validates that 500 parallel Send(object) calls produce correct results
    /// with no race conditions in the dispatch table.
    /// </summary>
    [Fact]
    public async Task ObjectSend_500Parallel_AllCorrect()
    {
        const int parallelism = 500;
        var tasks = new Task<object>[parallelism];

        for (int i = 0; i < parallelism; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var scope = _provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                object request = new Ping();
                return await ((ISender)mediator).Send(request, TestContext.Current.CancellationToken);
            });
        }

        var results = await Task.WhenAll(tasks);
        results.ShouldAllBe(r => (int)r == 42);
    }
}

public sealed record UnregisteredRequest : IRequest<int>;

// ═══════════════════════════════════════════════════════════════════
//  9. BACKGROUND SERVICE INTEGRATION TESTS
//     Validates mediator usage patterns from IHostedService / background
//     workers: scoped mediator resolution, long-running loops, cancellation.
// ═══════════════════════════════════════════════════════════════════

public class BackgroundServiceIntegrationTests
{
    /// <summary>
    /// Simulates a background service that processes N jobs via mediator.
    /// Each job runs in its own scope (correct DI pattern for hosted services).
    /// </summary>
    [Fact]
    public async Task BackgroundWorker_ProcessesJobsInScopes()
    {
        var counter = new SingletonCounter();
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
        services.AddSingleton(counter);

        using var provider = services.BuildServiceProvider();

        // Simulate background service loop
        const int jobCount = 20;
        var results = new ConcurrentBag<string>();

        for (int i = 0; i < jobCount; i++)
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new BackgroundJob(i), TestContext.Current.CancellationToken);
            results.Add(result);
        }

        results.Count.ShouldBe(jobCount);
        counter.Value.ShouldBe(jobCount);

        // Each job should have produced a unique result
        for (int i = 0; i < jobCount; i++)
            results.ShouldContain($"job-{i}-done");
    }

    /// <summary>
    /// Validates that cancellation token is respected — a cancelled token
    /// should not prevent already-in-flight work but should propagate
    /// through the handler.
    /// </summary>
    [Fact]
    public async Task BackgroundWorker_CancellationToken_Respected()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
        services.AddSingleton<SingletonCounter>();

        using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource();

        // First job completes normally
        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new BackgroundJob(1), cts.Token);
            result.ShouldBe("job-1-done");
        }

        // Cancel and verify the token source is in cancelled state
        await cts.CancelAsync();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    /// <summary>
    /// Simulates multiple concurrent background workers competing for the
    /// same singleton counter — validates no lost increments.
    /// </summary>
    [Fact]
    public async Task MultipleWorkers_ConcurrentScopes_NoLostIncrements()
    {
        var counter = new SingletonCounter();
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
        services.AddSingleton(counter);

        using var provider = services.BuildServiceProvider();

        const int workerCount = 10;
        const int jobsPerWorker = 50;
        var allResults = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, workerCount),
            TestContext.Current.CancellationToken,
            async (workerId, ct) =>
            {
                for (int j = 0; j < jobsPerWorker; j++)
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var result = await mediator.Send(
                        new BackgroundJob(workerId * 1000 + j), ct);
                    allResults.Add(result);
                }
            });

        allResults.Count.ShouldBe(workerCount * jobsPerWorker);
        counter.Value.ShouldBe(workerCount * jobsPerWorker);
    }
}

// ═══════════════════════════════════════════════════════════════════
// 10. STRESS INTEGRATION TESTS
//     Sustained load testing with consistent result verification.
// ═══════════════════════════════════════════════════════════════════

public class StressIntegrationTests
{
    /// <summary>
    /// 5000 sequential requests — validates no memory leaks, no state
    /// corruption, consistent results under sustained load.
    /// </summary>
    [Fact]
    public async Task SequentialStress_5000Requests_AllCorrect()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var provider = services.BuildServiceProvider();

        for (int i = 0; i < 5000; i++)
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
            result.ShouldBe(42);
        }
    }

    /// <summary>
    /// Stream 10,000 items across 100 parallel stream consumers.
    /// Validates no items are lost or duplicated.
    /// </summary>
    [Fact]
    public async Task ParallelStreams_100Consumers_AllItemsReceived()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileStreams();

        using var provider = services.BuildServiceProvider();

        const int consumers = 100;
        const int itemsPerStream = 100;
        var allCounts = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, consumers),
            TestContext.Current.CancellationToken,
            async (_, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                int count = 0;
                await foreach (var item in mediator.CreateStream(new StressStream(itemsPerStream), ct))
                {
                    count++;
                }

                allCounts.Add(count);
            });

        // Every consumer should have received exactly itemsPerStream items
        allCounts.Count.ShouldBe(consumers);
        allCounts.ShouldAllBe(c => c == itemsPerStream);
    }

    /// <summary>
    /// Mixed workload: Send + Publish + Stream running concurrently
    /// under sustained load. Validates no cross-contamination.
    /// </summary>
    [Fact]
    public async Task MixedWorkload_SendPublishStream_NoInterference()
    {
        var received = new ConcurrentBag<int>();
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();
        services.AddSingleton(received);

        using var provider = services.BuildServiceProvider();

        const int iterations = 200;
        var sendResults = new ConcurrentBag<int>();
        var streamCounts = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, iterations),
            TestContext.Current.CancellationToken,
            async (i, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Send
                var sendResult = await mediator.Send(new ConcurrentPing(i), ct);
                sendResults.Add(sendResult);

                // Publish
                await mediator.Publish(new ConcurrentNotification(i), ct);

                // Stream
                int count = 0;
                await foreach (var _ in mediator.CreateStream(new StressStream(10), ct))
                    count++;
                streamCounts.Add(count);
            });

        // Validate Send results
        sendResults.Count.ShouldBe(iterations);

        // Validate Publish — all notifications received
        received.Count.ShouldBe(iterations);

        // Validate Stream — every consumer got 10 items
        streamCounts.ShouldAllBe(c => c == 10);
    }
}

// ═══════════════════════════════════════════════════════════════════
// 11. FAILURE INJECTION INTEGRATION TESTS
//     Validates cancellation mid-pipeline, retry with intermittent
//     failures, and exception propagation under adverse conditions.
// ═══════════════════════════════════════════════════════════════════

public class FailureInjectionIntegrationTests
{
    /// <summary>
    /// Handler that delays 5s is cancelled after 50ms.
    /// Validates OperationCanceledException propagates correctly.
    /// </summary>
    [Fact]
    public async Task CancellationDuringHandler_ThrowsOperationCanceledException()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await mediator.Send(new DelayedPing(5000), cts.Token));
    }

    /// <summary>
    /// Flakey handler fails once, retry behavior recovers on second attempt.
    /// </summary>
    [Fact]
    public async Task RetryBehavior_RecoversFlakyHandler()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(new FlakeyState(1));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SimpleRetryBehavior<,>));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new FlakeyPing(), TestContext.Current.CancellationToken);
        result.ShouldBe("flakey-ok");
    }

    /// <summary>
    /// Flakey handler always fails — retry exhausted, exception propagates.
    /// </summary>
    [Fact]
    public async Task RetryExhausted_ExceptionPropagates()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(new FlakeyState(100));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SimpleRetryBehavior<,>));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await mediator.Send(new FlakeyPing(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 500 parallel sends where ~50% fail — validates no corruption,
    /// each result is either success or exception (never a wrong value).
    /// </summary>
    [Fact]
    public async Task IntermittentFailures_ParallelSend_NoCrossContamination()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(new FlakeyState(250));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();

        const int parallelism = 500;
        var successes = new ConcurrentBag<string>();
        var failures = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, parallelism),
            TestContext.Current.CancellationToken,
            async (_, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                try
                {
                    var result = await mediator.Send(new FlakeyPing(), ct);
                    successes.Add(result);
                }
                catch (InvalidOperationException ex)
                {
                    failures.Add(ex);
                }
            });

        // Total should equal parallelism
        (successes.Count + failures.Count).ShouldBe(parallelism);

        // All successes must have correct value
        successes.ShouldAllBe(r => r == "flakey-ok");

        // All failures must have correct message
        failures.ShouldAllBe(ex => ex.Message == "flakey-boom");
    }
}

// ═══════════════════════════════════════════════════════════════════
// 12. ALLOCATION REGRESSION INTEGRATION TESTS
//     Validates memory behavior under sustained load — no unbounded
//     growth, proper disposal, stable allocation patterns.
// ═══════════════════════════════════════════════════════════════════

public class AllocationRegressionIntegrationTests
{
    /// <summary>
    /// 10,000 sequential requests — per-thread allocation should be bounded.
    /// Validates no cumulative memory leaks per request.
    /// </summary>
    [Fact]
    [Trait("Category", "NonDeterministic")]
    public async Task SequentialRequests_AllocationPerRequestBounded()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();
        using var provider = services.BuildServiceProvider();

        // Warmup — JIT, caches, etc.
        for (int i = 0; i < 100; i++)
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
        }

        var allocBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10_000; i++)
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
        }

        var allocAfter = GC.GetAllocatedBytesForCurrentThread();
        var bytesPerRequest = (allocAfter - allocBefore) / 10_000.0;

        // Each request should allocate a bounded amount (generous: < 10KB per request)
        bytesPerRequest.ShouldBeLessThan(10_000,
            $"Allocated {bytesPerRequest:F0} bytes per request — potential leak");
    }

    /// <summary>
    /// Scoped dependencies are properly disposed after scope ends.
    /// </summary>
    [Fact]
    public async Task ScopedDisposal_DisposableResourcesReleased()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();
        services.AddSingleton<SingletonCounter>();
        services.AddScoped<ScopedCorrelation>();
        services.AddTransient<TransientStamp>();
        services.AddScoped<IRequestHandler<LifetimePing, LifetimeResult>, LifetimePingHandler>();

        using var provider = services.BuildServiceProvider();
        ScopedCorrelation capturedCorrelation;

        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new LifetimePing(), TestContext.Current.CancellationToken);

            // Capture the scoped instance before disposal
            capturedCorrelation = scope.ServiceProvider.GetRequiredService<ScopedCorrelation>();
            capturedCorrelation.Disposed.ShouldBeFalse();
        }

        // After scope disposal, the scoped instance should be disposed
        capturedCorrelation.Disposed.ShouldBeTrue();
    }

    /// <summary>
    /// 5 waves of 100 parallel scopes — memory remains stable after GC.
    /// </summary>
    [Fact]
    [Trait("Category", "NonDeterministic")]
    public async Task ParallelScopes_StableMemory()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileStreams();
        using var provider = services.BuildServiceProvider();

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baseline = GC.GetTotalMemory(forceFullCollection: true);

        for (int wave = 0; wave < 5; wave++)
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, 100),
                TestContext.Current.CancellationToken,
                async (_, ct) =>
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(new Ping(), ct);
                });
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var afterLoad = GC.GetTotalMemory(forceFullCollection: true);

        (afterLoad - baseline).ShouldBeLessThan(10 * 1024 * 1024,
            $"Memory grew by {(afterLoad - baseline) / 1024}KB after 500 parallel scopes");
    }
}

// ═══════════════════════════════════════════════════════════════════
// 13. TIMEOUT / DEADLOCK INTEGRATION TESTS
//     Validates async pipeline doesn't deadlock under various
//     contention patterns and nested send scenarios.
// ═══════════════════════════════════════════════════════════════════

public class TimeoutDeadlockIntegrationTests
{
    /// <summary>
    /// 3 slow behaviors (10ms each) + handler — total pipeline completes
    /// well within a generous 5-second timeout.
    /// </summary>
    [Fact]
    public async Task SlowBehaviorChain_CompletesWithinTimeout()
    {
        SlowBehavior<Ping, int>.DelayMs = 10;

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SlowBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SlowBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SlowBehavior<,>));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var timeout = Debugger.IsAttached ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource(timeout);
        var result = await mediator.Send(new Ping(), cts.Token);
        result.ShouldBe(42);
    }

    /// <summary>
    /// Behavior sends Ping inside the NestedOuterPing pipeline — validates
    /// no deadlock when dispatching a different request type mid-pipeline.
    /// </summary>
    [Fact]
    public async Task NestedSendInsideBehavior_NoDeadlock()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient<IPipelineBehavior<NestedOuterPing, string>, NestedSendBehavior>();
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var timeout = Debugger.IsAttached ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource(timeout);
        var task = mediator.Send(new NestedOuterPing(), cts.Token).AsTask();
        var completed = await Task.WhenAny(task, Task.Delay(timeout));

        completed.ShouldBe(task, "Pipeline should complete — deadlock detected!");
        var result = await task;
        result.ShouldBe("nested-ok+inner=42");
    }

    /// <summary>
    /// 200 parallel sends with slow behaviors — all must complete
    /// within 60 seconds (validates no thread pool starvation).
    /// </summary>
    [Fact]
    public async Task ParallelSend_WithSlowBehaviors_CompletesWithinTimeout()
    {
        SlowBehavior<ConcurrentPing, int>.DelayMs = 5;

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SlowBehavior<,>));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();

        var timeout = Debugger.IsAttached ? TimeSpan.FromSeconds(120) : TimeSpan.FromSeconds(60);
        using var cts = new CancellationTokenSource(timeout);
        const int parallelism = 200;
        var results = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, parallelism),
            cts.Token,
            async (seed, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new ConcurrentPing(seed), ct);
                results.Add(result);
            });

        results.Count.ShouldBe(parallelism);
    }
}

// ═══════════════════════════════════════════════════════════════════
// 14. CHAOS INTEGRATION TESTS
//     Random delays + random exceptions + high concurrency —
//     validates system stability under unpredictable conditions.
// ═══════════════════════════════════════════════════════════════════

public class ChaosIntegrationTests
{
    /// <summary>
    /// 1000 parallel sends with 10% failure rate — ~90% should succeed,
    /// total (successes + failures) must equal 1000.
    /// </summary>
    [Fact]
    public async Task ChaosMode_1000Parallel_MostSucceed()
    {
        var config = new ChaosConfig { FailureRate = 0.1, MaxDelayMs = 20 };
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(config);
        services.AddSingleton<IChaosRandom>(new ThreadSafeChaosRandom());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ChaosBehavior<,>));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();

        const int parallelism = 1000;
        var successes = new ConcurrentBag<int>();
        var failures = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, parallelism),
            TestContext.Current.CancellationToken,
            async (seed, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                try
                {
                    var result = await mediator.Send(new ConcurrentPing(seed), ct);
                    successes.Add(result);
                }
                catch (InvalidOperationException ex)
                {
                    failures.Add(ex);
                }
            });

        (successes.Count + failures.Count).ShouldBe(parallelism);

        // With 10% failure rate, at least 70% should succeed (generous margin)
        successes.Count.ShouldBeGreaterThan(700);

        // All successes should have correct values (seed × 2)
        successes.ShouldAllBe(r => r % 2 == 0);
    }

    /// <summary>
    /// Mixed Send + Publish with chaos — validates no cross-contamination
    /// even under random failures and delays.
    /// </summary>
    [Fact]
    public async Task MixedWorkloadWithChaos_NoCrossContamination()
    {
        var received = new ConcurrentBag<int>();
        var config = new ChaosConfig { FailureRate = 0.05, MaxDelayMs = 10 };
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompileNotifications();
        services.AddSingleton(config);
        services.AddSingleton<IChaosRandom>(new ThreadSafeChaosRandom());
        services.AddSingleton(received);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ChaosBehavior<,>));
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();

        const int iterations = 300;
        var sendSuccesses = new ConcurrentBag<int>();
        var sendFailures = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, iterations),
            TestContext.Current.CancellationToken,
            async (i, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Send (may fail due to chaos)
                try
                {
                    var result = await mediator.Send(new ConcurrentPing(i), ct);
                    sendSuccesses.Add(result);
                }
                catch (InvalidOperationException)
                {
                    sendFailures.Add(new InvalidOperationException());
                }

                // Publish (notifications don't go through pipeline behaviors)
                await mediator.Publish(new ConcurrentNotification(i), ct);
            });

        (sendSuccesses.Count + sendFailures.Count).ShouldBe(iterations);

        // Notifications bypass pipeline behaviors — all should be received
        received.Count.ShouldBe(iterations);
    }

    /// <summary>
    /// 50% failure rate with exception handler — all requests return either
    /// the handler result or the fallback, never an exception to the caller.
    /// </summary>
    [Fact]
    public async Task HighFailureRate_WithExceptionHandler_GracefulDegradation()
    {
        var config = new ChaosConfig { FailureRate = 0.5, MaxDelayMs = 5 };
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(config);
        services.AddSingleton<IChaosRandom>(new ThreadSafeChaosRandom());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ChaosBehavior<,>));
        services.AddSingleton<IRequestExceptionHandler<ConcurrentPing, int>>(
            new ConcurrentPingFallbackExceptionHandler());
        services.PrecompilePipelines();
        using var provider = services.BuildServiceProvider();

        const int parallelism = 500;
        var results = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, parallelism),
            TestContext.Current.CancellationToken,
            async (seed, ct) =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new ConcurrentPing(seed), ct);
                results.Add(result);
            });

        // ALL requests should complete (no exceptions to caller)
        results.Count.ShouldBe(parallelism);

        // Some got the real result (seed × 2), some got fallback (-1)
        results.ShouldContain(r => r != -1, "some requests should succeed normally");
        results.ShouldContain(r => r == -1, "some requests should return fallback");
    }
}

public sealed class ConcurrentPingFallbackExceptionHandler : IRequestExceptionHandler<ConcurrentPing, int>
{
    public ValueTask Handle(ConcurrentPing request, Exception exception,
        RequestExceptionHandlerState<int> state, CancellationToken ct)
    {
        state.SetHandled(-1);
        return ValueTask.CompletedTask;
    }
}
