// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Request types + handlers (the generator registers these) ────────

public sealed record ObservedPing : IRequest<int>;
public sealed record ObservedThrowPing : IRequest<int>;
// Used ONLY by the handler-only test — never with pipeline components anywhere — so its process-global
// RequestDispatch flag is not polluted by other tests that add pre/post to a shared request type.
public sealed record ObservedSoloPing : IRequest<int>;

public sealed class ObservedSoloPingHandler(List<string> log) : IRequestHandler<ObservedSoloPing, int>
{
    public ValueTask<int> Handle(ObservedSoloPing request, CancellationToken ct)
    {
        log.Add("handler");
        return new(7);
    }
}

public sealed class ObservedPingHandler(List<string> log) : IRequestHandler<ObservedPing, int>
{
    public ValueTask<int> Handle(ObservedPing request, CancellationToken ct)
    {
        log.Add("handler");
        return new(42);
    }
}

public sealed class ObservedThrowPingHandler(List<string> log) : IRequestHandler<ObservedThrowPing, int>
{
    public ValueTask<int> Handle(ObservedThrowPing request, CancellationToken ct)
    {
        log.Add("handler");
        throw new InvalidOperationException("boom");
    }
}

// ── Pre/Post processors that log into the shared order log ──────────

public sealed class LoggingPreProcessor<TRequest>(List<string> log) : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken ct)
    {
        log.Add("pre");
        return ValueTask.CompletedTask;
    }
}

public sealed class LoggingPostProcessor<TRequest, TResponse>(List<string> log) : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        log.Add("post");
        return ValueTask.CompletedTask;
    }
}

// ── Fake dispatch observer that records the dispatch lifecycle ──────

public sealed class RecordingObserver(List<string> log) : IMediatorDispatchObserver
{
    public bool Active { get; set; } = true;

    public bool IsActive => Active;

    public IMediatorDispatchScope? BeginDispatch<TRequest, TResponse>(TRequest request, IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        log.Add($"observer:begin:{typeof(TRequest).Name}:{handler.GetType().Name}");
        return new RecordingScope(log);
    }

    private sealed class RecordingScope(List<string> log) : IMediatorDispatchScope
    {
        public void OnError(Exception exception) => log.Add($"observer:error:{exception.GetType().Name}");
        public void Dispose() => log.Add("observer:dispose");
    }
}

// ── Tests ───────────────────────────────────────────────────────────

/// <summary>
/// Verifies the core's <see cref="IMediatorDispatchObserver"/> wiring: an active observer's scope wraps the
/// WHOLE pipeline (pre-/post-processors included — the reason a behavior could not do this), an inactive or
/// absent observer leaves the fast path untouched, and an unhandled failure is reported before the scope is
/// disposed. The hot-path 0-overhead of the null-observer case is proven separately by the benchmark suite.
/// </summary>
public class DispatchObserverTests
{
    private static ServiceCollection BuildServices(List<string> log)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(log);
        return services;
    }

    [Fact]
    public async Task Active_observer_scope_wraps_the_whole_pipeline_including_pre_and_post_processors()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        services.AddSingleton<IRequestPreProcessor<ObservedPing>>(_ => new LoggingPreProcessor<ObservedPing>(log));
        services.AddSingleton<IRequestPostProcessor<ObservedPing, int>>(_ => new LoggingPostProcessor<ObservedPing, int>(log));
        services.AddSingleton<IMediatorDispatchObserver>(new RecordingObserver(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ObservedPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        // The scope opens BEFORE the pre-processor and closes AFTER the post-processor — the span nests the
        // entire dispatch, which a pipeline behavior (running only inside the behavior chain) cannot achieve.
        log.ShouldBe(new[]
        {
            "observer:begin:ObservedPing:ObservedPingHandler",
            "pre",
            "handler",
            "post",
            "observer:dispose",
        });
    }

    [Fact]
    public async Task Active_observer_wraps_a_handler_only_request_with_no_pipeline_components()
    {
        // The COMMON case: a request with NO behaviors/pre/post/exception handlers. The observer must still
        // wrap it — otherwise handler-only dispatches (the majority) would never be traced.
        var log = new List<string>();
        var services = BuildServices(log);
        services.AddSingleton<IMediatorDispatchObserver>(new RecordingObserver(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ObservedSoloPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(7);
        log.ShouldBe(new[] { "observer:begin:ObservedSoloPing:ObservedSoloPingHandler", "handler", "observer:dispose" });
    }

    [Fact]
    public async Task Inactive_observer_is_not_invoked()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        services.AddSingleton<IRequestPreProcessor<ObservedPing>>(_ => new LoggingPreProcessor<ObservedPing>(log));
        services.AddSingleton<IRequestPostProcessor<ObservedPing, int>>(_ => new LoggingPostProcessor<ObservedPing, int>(log));
        services.AddSingleton<IMediatorDispatchObserver>(new RecordingObserver(log) { Active = false });
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ObservedPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        // Registered but inactive → the dispatch never enters the observed path; no begin/dispose recorded.
        log.ShouldBe(new[] { "pre", "handler", "post" });
    }

    [Fact]
    public async Task Unhandled_exception_is_reported_to_the_scope_then_disposed()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        services.AddSingleton<IRequestPreProcessor<ObservedThrowPing>>(_ => new LoggingPreProcessor<ObservedThrowPing>(log));
        services.AddSingleton<IRequestPostProcessor<ObservedThrowPing, int>>(_ => new LoggingPostProcessor<ObservedThrowPing, int>(log));
        services.AddSingleton<IMediatorDispatchObserver>(new RecordingObserver(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await mediator.Send(new ObservedThrowPing(), TestContext.Current.CancellationToken));

        // OnError fires before Dispose; the post-processor never runs (the dispatch failed).
        log.ShouldBe(new[]
        {
            "observer:begin:ObservedThrowPing:ObservedThrowPingHandler",
            "pre",
            "handler",
            "observer:error:InvalidOperationException",
            "observer:dispose",
        });
    }

    [Fact]
    public async Task No_observer_registered_runs_the_pipeline_normally()
    {
        var log = new List<string>();
        var services = BuildServices(log);
        services.AddSingleton<IRequestPreProcessor<ObservedPing>>(_ => new LoggingPreProcessor<ObservedPing>(log));
        services.AddSingleton<IRequestPostProcessor<ObservedPing, int>>(_ => new LoggingPostProcessor<ObservedPing, int>(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ObservedPing(), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        log.ShouldBe(new[] { "pre", "handler", "post" });
    }
}
