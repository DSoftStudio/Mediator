// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════
//  TEST-LOCAL TYPES — avoid cross-test static pollution
// ═══════════════════════════════════════════════════════════════════

public sealed record BuilderPing() : IRequest<string>;

public sealed class BuilderPingHandler : IRequestHandler<BuilderPing, string>
{
    public ValueTask<string> Handle(BuilderPing request, CancellationToken ct)
        => new("builder-ok");
}

public sealed record BuilderPingWithBehavior() : IRequest<string>;

public sealed class BuilderPingWithBehaviorHandler : IRequestHandler<BuilderPingWithBehavior, string>
{
    public ValueTask<string> Handle(BuilderPingWithBehavior request, CancellationToken ct)
        => new("handler-reached");
}

public sealed class BuilderTrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;

    public BuilderTrackingBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        _log.Add("tracking:before");
        var result = await next.Handle(request, ct);
        _log.Add("tracking:after");
        return result;
    }
}

public sealed record BuilderNotification() : INotification;

public sealed class BuilderNotificationHandlerA : INotificationHandler<BuilderNotification>
{
    private readonly List<string> _log;
    public BuilderNotificationHandlerA(List<string> log) => _log = log;

    public Task Handle(BuilderNotification notification, CancellationToken ct)
    {
        _log.Add("A");
        return Task.CompletedTask;
    }
}

public sealed class BuilderNotificationHandlerB : INotificationHandler<BuilderNotification>
{
    private readonly List<string> _log;
    public BuilderNotificationHandlerB(List<string> log) => _log = log;

    public Task Handle(BuilderNotification notification, CancellationToken ct)
    {
        _log.Add("B");
        return Task.CompletedTask;
    }
}

public sealed record BuilderPreProcPing() : IRequest<string>;

public sealed class BuilderPreProcPingHandler : IRequestHandler<BuilderPreProcPing, string>
{
    public ValueTask<string> Handle(BuilderPreProcPing request, CancellationToken ct)
        => new("preproc-ok");
}

public sealed class BuilderPreProcessor : IRequestPreProcessor<BuilderPreProcPing>
{
    private readonly List<string> _log;
    public BuilderPreProcessor(List<string> log) => _log = log;

    public ValueTask Process(BuilderPreProcPing request, CancellationToken ct)
    {
        _log.Add("pre");
        return default;
    }
}

public sealed class BuilderPostProcessor : IRequestPostProcessor<BuilderPreProcPing, string>
{
    private readonly List<string> _log;
    public BuilderPostProcessor(List<string> log) => _log = log;

    public ValueTask Process(BuilderPreProcPing request, string response, CancellationToken ct)
    {
        _log.Add("post");
        return default;
    }
}

public sealed record BuilderStreamPing() : IStreamRequest<string>;

public sealed class BuilderStreamPingHandler : IStreamRequestHandler<BuilderStreamPing, string>
{
    public async IAsyncEnumerable<string> Handle(BuilderStreamPing request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield return "stream-ok";
    }
}

public sealed class BuilderStreamBehavior : IStreamPipelineBehavior<BuilderStreamPing, string>
{
    private readonly List<string> _log;
    public BuilderStreamBehavior(List<string> log) => _log = log;

    public async IAsyncEnumerable<string> Handle(
        BuilderStreamPing request,
        IStreamRequestHandler<BuilderStreamPing, string> next,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        _log.Add("stream:before");
        await foreach (var item in next.Handle(request, ct))
        {
            yield return item;
        }
        _log.Add("stream:after");
    }
}

public sealed record BuilderObserverPing() : IRequest<string>;

public sealed class BuilderObserverPingHandler : IRequestHandler<BuilderObserverPing, string>
{
    public ValueTask<string> Handle(BuilderObserverPing request, CancellationToken ct)
        => new("observed-ok");
}

public sealed class BuilderDispatchObserver(List<string> log) : IMediatorDispatchObserver
{
    public bool IsActive => true;

    public IMediatorDispatchScope? BeginDispatch<TRequest, TResponse>(TRequest request, IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        log.Add("begin");
        return new Scope(log);
    }

    private sealed class Scope(List<string> log) : IMediatorDispatchScope
    {
        public void OnError(Exception exception) => log.Add("error");
        public void Dispose() => log.Add("dispose");
    }
}

public sealed record BuilderExcPing() : IRequest<string>;

public sealed class BuilderExcPingHandler : IRequestHandler<BuilderExcPing, string>
{
    public ValueTask<string> Handle(BuilderExcPing request, CancellationToken ct)
        => new("exc-ok");
}

public sealed class BuilderExceptionHandler : IRequestExceptionHandler<BuilderExcPing, string>
{
    public ValueTask Handle(BuilderExcPing request, Exception exception,
        RequestExceptionHandlerState<string> state, CancellationToken ct)
    {
        state.SetHandled("handled-fallback");
        return default;
    }
}

// ═══════════════════════════════════════════════════════════════════
//  TEST CLASSES
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Integration tests for <see cref="MediatorBuilder"/> and the
/// <c>AddMediator(Action&lt;MediatorBuilder&gt;)</c> generated overload.
/// Validates end-to-end dispatch, builder method registration, idempotency guards,
/// and parallel test isolation with independent <see cref="ServiceCollection"/> instances.
/// </summary>
public class MediatorBuilderIntegrationTests
{
    /// <summary>
    /// AddMediator(configure) registers handlers and precompiles pipelines —
    /// a basic Send() should resolve and return the expected result.
    /// </summary>
    [Fact]
    public async Task AddMediator_WithBuilder_DispatchesRequest()
    {
        var services = new ServiceCollection();
        services.AddMediator(builder => { });

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderPing(), TestContext.Current.CancellationToken);

        result.ShouldBe("builder-ok");
    }

    /// <summary>
    /// AddMediator(configure) + AddOpenBehavior registers a pipeline behavior
    /// that wraps handler execution.
    /// </summary>
    [Fact]
    public async Task AddMediator_WithOpenBehavior_ExecutesPipeline()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(builder =>
        {
            builder.AddOpenBehavior(typeof(BuilderTrackingBehavior<,>));
        });

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderPingWithBehavior(), TestContext.Current.CancellationToken);

        result.ShouldBe("handler-reached");
        log.ShouldBe(new[] { "tracking:before", "tracking:after" });
    }

    /// <summary>
    /// AddMediator(configure) + AddRequestPreProcessor registers a pre-processor
    /// that executes before the handler.
    /// </summary>
    [Fact]
    public async Task AddMediator_WithPreProcessor_ExecutesBefore()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(builder =>
        {
            builder.AddRequestPreProcessor<BuilderPreProcessor>();
        });

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderPreProcPing(), TestContext.Current.CancellationToken);

        result.ShouldBe("preproc-ok");
        log.ShouldContain("pre");
    }

    /// <summary>
    /// AddMediator(configure) + AddDispatchObserver&lt;T&gt; registers a dispatch observer that wraps the whole
    /// dispatch — even a handler-only request with no behaviors/processors (the builder + generator force a
    /// pipeline chain so the observer is not bypassed).
    /// </summary>
    [Fact]
    public async Task AddMediator_WithDispatchObserverType_WrapsTheDispatch()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(builder => builder.AddDispatchObserver<BuilderDispatchObserver>());

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderObserverPing(), TestContext.Current.CancellationToken);

        result.ShouldBe("observed-ok");
        log.ShouldBe(new[] { "begin", "dispose" });
    }

    /// <summary>
    /// AddMediator(configure) + AddDispatchObserver(instance) registers a pre-configured observer instance.
    /// </summary>
    [Fact]
    public async Task AddMediator_WithDispatchObserverInstance_WrapsTheDispatch()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator(builder => builder.AddDispatchObserver(new BuilderDispatchObserver(log)));

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderObserverPing(), TestContext.Current.CancellationToken);

        result.ShouldBe("observed-ok");
        log.ShouldBe(new[] { "begin", "dispose" });
    }

    /// <summary>
    /// AddMediator(configure) + AddRequestPostProcessor registers a post-processor
    /// that executes after the handler.
    /// </summary>
    [Fact]
    public async Task AddMediator_WithPostProcessor_ExecutesAfter()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(builder =>
        {
            builder.AddRequestPostProcessor<BuilderPostProcessor>();
        });

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderPreProcPing(), TestContext.Current.CancellationToken);

        result.ShouldBe("preproc-ok");
        log.ShouldContain("post");
    }

    /// <summary>
    /// AddMediator(configure) + AddParallelNotificationPublisher replaces the
    /// sequential publisher with the parallel implementation.
    /// </summary>
    [Fact]
    public async Task AddMediator_WithParallelPublisher_PublishesNotification()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddMediator(builder =>
        {
            builder.AddParallelNotificationPublisher();
        });

        await using var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<IPublisher>();

        await publisher.Publish(new BuilderNotification(), TestContext.Current.CancellationToken);

        log.ShouldContain("A");
        log.ShouldContain("B");
    }

    /// <summary>
    /// Calling RegisterMediatorHandlers() twice on the same ServiceCollection
    /// should NOT produce duplicate handler registrations (sentinel guard).
    /// </summary>
    [Fact]
    public async Task RegisterMediatorHandlers_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        services
            .AddMediator()
            .RegisterMediatorHandlers()
            .RegisterMediatorHandlers() // second call — should be idempotent
            .PrecompilePipelines();

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        // Should still work — no duplicate handler exception
        var result = await sender.Send(new BuilderPing(), TestContext.Current.CancellationToken);
        result.ShouldBe("builder-ok");

        // Verify only one handler registration exists (not duplicated)
        var handlerDescriptors = services
            .Where(d => d.ServiceType == typeof(IRequestHandler<BuilderPing, string>))
            .ToList();
        handlerDescriptors.Count.ShouldBe(1);
    }

    /// <summary>
    /// Calling PrecompilePipelines() twice on the same ServiceCollection
    /// should NOT produce duplicate pipeline chain registrations (sentinel guard).
    /// </summary>
    [Fact]
    public async Task PrecompilePipelines_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        services
            .AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompilePipelines(); // second call — should be idempotent

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderPing(), TestContext.Current.CancellationToken);
        result.ShouldBe("builder-ok");
    }

    /// <summary>
    /// Two completely independent ServiceCollections configured differently
    /// should not interfere with each other (no static state leaking).
    /// Validates that the per-IServiceCollection sentinel pattern works correctly
    /// in parallel test scenarios.
    /// </summary>
    [Fact]
    public async Task ParallelIsolation_IndependentServiceCollections_DoNotInterfere()
    {
        // Collection A: with behavior
        var logA = new List<string>();
        var servicesA = new ServiceCollection();
        servicesA.AddSingleton(logA);
        servicesA.AddMediator(builder =>
        {
            builder.AddOpenBehavior(typeof(BuilderTrackingBehavior<,>));
        });

        // Collection B: without behavior
        var servicesB = new ServiceCollection();
        servicesB.AddMediator(builder => { });

        await using var spA = servicesA.BuildServiceProvider();
        await using var spB = servicesB.BuildServiceProvider();

        var senderA = spA.GetRequiredService<ISender>();
        var senderB = spB.GetRequiredService<ISender>();

        var ct = TestContext.Current.CancellationToken;

        // Both should dispatch successfully
        var resultA = await senderA.Send(new BuilderPingWithBehavior(), ct);
        var resultB = await senderB.Send(new BuilderPingWithBehavior(), ct);

        resultA.ShouldBe("handler-reached");
        resultB.ShouldBe("handler-reached");

        // Only A should have behavior log entries
        logA.ShouldBe(new[] { "tracking:before", "tracking:after" });
    }

    /// <summary>
    /// AddMediator(configure) followed by a second AddMediator(configure) call
    /// should be idempotent: handlers are registered once, pipelines are compiled once.
    /// </summary>
    [Fact]
    public async Task AddMediator_CalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddMediator(builder => { });
        services.AddMediator(builder => { }); // second call — sentinel prevents re-registration

        await using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new BuilderPing(), TestContext.Current.CancellationToken);
        result.ShouldBe("builder-ok");

        // Only one handler registration
        var handlerDescriptors = services
            .Where(d => d.ServiceType == typeof(IRequestHandler<BuilderPing, string>))
            .ToList();
        handlerDescriptors.Count.ShouldBe(1);
    }

    /// <summary>
    /// AddOpenBehavior rejects non-generic types with ArgumentException.
    /// </summary>
    [Fact]
    public void AddOpenBehavior_NonGenericType_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        Should.Throw<ArgumentException>(() =>
            builder.AddOpenBehavior(typeof(string)));
    }

    /// <summary>
    /// MediatorBuilder constructor rejects null ServiceCollection.
    /// </summary>
    [Fact]
    public void MediatorBuilder_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MediatorBuilder(null!));
    }

    /// <summary>
    /// AddStreamBehavior registers a closed stream pipeline behavior
    /// that wraps stream handler execution.
    /// </summary>
    [Fact]
    public void AddStreamBehavior_ValidType_RegistersService()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        builder.AddStreamBehavior<BuilderStreamBehavior>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IStreamPipelineBehavior<BuilderStreamPing, string>)
            && d.ImplementationType == typeof(BuilderStreamBehavior));
    }

    /// <summary>
    /// AddStreamBehavior rejects types that do not implement IStreamPipelineBehavior.
    /// </summary>
    [Fact]
    public void AddStreamBehavior_InvalidType_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        Should.Throw<ArgumentException>(() =>
            builder.AddStreamBehavior<BuilderPingHandler>());
    }

    /// <summary>
    /// AddRequestExceptionHandler registers a closed exception handler.
    /// </summary>
    [Fact]
    public void AddRequestExceptionHandler_ValidType_RegistersService()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        builder.AddRequestExceptionHandler<BuilderExceptionHandler>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IRequestExceptionHandler<BuilderExcPing, string>)
            && d.ImplementationType == typeof(BuilderExceptionHandler));
    }

    /// <summary>
    /// AddRequestExceptionHandler rejects types that do not implement IRequestExceptionHandler.
    /// </summary>
    [Fact]
    public void AddRequestExceptionHandler_InvalidType_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        Should.Throw<ArgumentException>(() =>
            builder.AddRequestExceptionHandler<BuilderPingHandler>());
    }

    /// <summary>
    /// AddRequestPreProcessor rejects types that do not implement IRequestPreProcessor.
    /// </summary>
    [Fact]
    public void AddRequestPreProcessor_InvalidType_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        Should.Throw<ArgumentException>(() =>
            builder.AddRequestPreProcessor<BuilderPingHandler>());
    }

    /// <summary>
    /// AddRequestPostProcessor rejects types that do not implement IRequestPostProcessor.
    /// </summary>
    [Fact]
    public void AddRequestPostProcessor_InvalidType_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        Should.Throw<ArgumentException>(() =>
            builder.AddRequestPostProcessor<BuilderPingHandler>());
    }

    /// <summary>
    /// AddOpenBehavior respects non-default ServiceLifetime.
    /// </summary>
    [Fact]
    public void AddOpenBehavior_WithScopedLifetime_RegistersWithCorrectLifetime()
    {
        var services = new ServiceCollection();
        var builder = new MediatorBuilder(services);

        builder.AddOpenBehavior(typeof(BuilderTrackingBehavior<,>), ServiceLifetime.Scoped);

        services.ShouldContain(d =>
            d.ServiceType == typeof(IPipelineBehavior<,>)
            && d.Lifetime == ServiceLifetime.Scoped);
    }
}
