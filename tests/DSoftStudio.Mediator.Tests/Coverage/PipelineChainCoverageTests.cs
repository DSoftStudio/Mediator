// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Unique types to avoid static dispatch collisions ──

public record CovAsyncPre : IRequest<int>;
public record CovAsyncPost : IRequest<int>;
public record CovAsyncPrePost : IRequest<int>;
public record CovExOnly : IRequest<int>;
public record CovPreExPost : IRequest<int>;
public record CovPassThrough : IRequest<int>;
public record CovPreOnly : IRequest<int>;

// ── Handlers ──

public sealed class CovAsyncPreHandler : IRequestHandler<CovAsyncPre, int>
{
    public ValueTask<int> Handle(CovAsyncPre request, CancellationToken ct) => new(1);
}

public sealed class CovAsyncPostHandler : IRequestHandler<CovAsyncPost, int>
{
    public async ValueTask<int> Handle(CovAsyncPost request, CancellationToken ct)
    {
        await Task.Yield();
        return 2;
    }
}

public sealed class CovAsyncPrePostHandler : IRequestHandler<CovAsyncPrePost, int>
{
    public ValueTask<int> Handle(CovAsyncPrePost request, CancellationToken ct) => new(3);
}

public sealed class CovExOnlyHandler : IRequestHandler<CovExOnly, int>
{
    public ValueTask<int> Handle(CovExOnly request, CancellationToken ct)
        => throw new InvalidOperationException("boom");
}

public sealed class CovPreExPostHandler : IRequestHandler<CovPreExPost, int>
{
    public ValueTask<int> Handle(CovPreExPost request, CancellationToken ct)
        => throw new InvalidOperationException("boom2");
}

public sealed class CovPassThroughHandler : IRequestHandler<CovPassThrough, int>
{
    public ValueTask<int> Handle(CovPassThrough request, CancellationToken ct) => new(99);
}

public sealed class CovPreOnlyHandler : IRequestHandler<CovPreOnly, int>
{
    public ValueTask<int> Handle(CovPreOnly request, CancellationToken ct) => new(44);
}

public sealed class SyncPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken ct) => ValueTask.CompletedTask;
}

// ── Async PreProcessor ──

public sealed class AsyncPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public async ValueTask Process(TRequest request, CancellationToken ct)
    {
        await Task.Yield();
    }
}

// ── Async PostProcessor ──

public sealed class AsyncPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public async ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        await Task.Yield();
    }
}

// ── Exception handler that marks as handled ──

public sealed class CovExHandler<TRequest, TResponse> : IRequestExceptionHandler<TRequest, TResponse>
{
    public ValueTask Handle(TRequest request, Exception exception,
        RequestExceptionHandlerState<TResponse> state, CancellationToken ct)
    {
        state.SetHandled(default!);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Tests for PipelineChainHandler paths: async pre-processors, async post-processors,
/// exception handlers only, pre+exception+post, and passthrough mode.
/// </summary>
public class PipelineChainHandlerCoverageTests
{
    [Fact]
    public async Task AsyncPreProcessor_TriggersAsyncPath()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovAsyncPre, int>, CovAsyncPreHandler>();
        services.AddTransient<IRequestPreProcessor<CovAsyncPre>, AsyncPreProcessor<CovAsyncPre>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovAsyncPre, int>(new CovAsyncPre());
        result.ShouldBe(1);
    }

    [Fact]
    public async Task AsyncPostProcessor_WithAsyncCore_TriggersAwaitCoreAndRunPostProcessors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovAsyncPost, int>, CovAsyncPostHandler>();
        services.AddTransient<IRequestPostProcessor<CovAsyncPost, int>, AsyncPostProcessor<CovAsyncPost, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovAsyncPost, int>(new CovAsyncPost());
        result.ShouldBe(2);
    }

    [Fact]
    public async Task AsyncPreProcessor_WithPostProcessor_TriggersHandleWithProcessorsAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovAsyncPrePost, int>, CovAsyncPrePostHandler>();
        services.AddTransient<IRequestPreProcessor<CovAsyncPrePost>, AsyncPreProcessor<CovAsyncPrePost>>();
        services.AddTransient<IRequestPostProcessor<CovAsyncPrePost, int>, AsyncPostProcessor<CovAsyncPrePost, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovAsyncPrePost, int>(new CovAsyncPrePost());
        result.ShouldBe(3);
    }

    [Fact]
    public async Task ExceptionHandlerOnly_CatchesAndHandles()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovExOnly, int>, CovExOnlyHandler>();
        services.AddTransient<IRequestExceptionHandler<CovExOnly, int>, CovExHandler<CovExOnly, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovExOnly, int>(new CovExOnly());
        result.ShouldBe(default(int));
    }

    [Fact]
    public async Task PreProcessor_ExceptionHandler_PostProcessor_FullPipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovPreExPost, int>, CovPreExPostHandler>();
        services.AddTransient<IRequestPreProcessor<CovPreExPost>, AsyncPreProcessor<CovPreExPost>>();
        services.AddTransient<IRequestExceptionHandler<CovPreExPost, int>, CovExHandler<CovPreExPost, int>>();
        services.AddTransient<IRequestPostProcessor<CovPreExPost, int>, AsyncPostProcessor<CovPreExPost, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovPreExPost, int>(new CovPreExPost());
        result.ShouldBe(default(int));
    }

    [Fact]
    public async Task PassThrough_NoPipelineComponents_DirectCall()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovPassThrough, int>, CovPassThroughHandler>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovPassThrough, int>(new CovPassThrough());
        result.ShouldBe(99);
    }

    [Fact]
    public async Task PreProcessorOnly_NoPostProcessor_ReturnsEarly()
    {
        // Covers HandleWithProcessors where _postProcessors.Length == 0 → return coreResult
        var services = new ServiceCollection();
        services.AddTransient<IRequestPreProcessor<CovPreOnly>, SyncPreProcessor<CovPreOnly>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovPreOnly, int>(new CovPreOnly());
        result.ShouldBe(44);
    }
}
