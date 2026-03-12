// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ══════════════════════════════════════════════════════════════════
//  Unique message types — each test gap gets its own types to avoid
//  static dispatch collisions with other tests.
// ══════════════════════════════════════════════════════════════════

// Gap 2: Exception handler that does NOT handle (re-throw path)
public record CovUnhandledEx : IRequest<int>;

// Gap 3: Async pre-processor + exception handler path
public record CovAsyncPreEx : IRequest<int>;

// Gap 4: HandleFull → exception handlers only (no processors)
public record CovExOnlyFull : IRequest<int>;

// Gap 6: AddMediator null services
// (no message type needed)

// Gap 8: Constructor array path
public record CovArrayCtor : IRequest<int>;

// Gap: HandleWithProcessorsAsync → sync remaining pre-processors after async one
public record CovMultiPreAsync : IRequest<int>;

// Gap: HandleWithProcessors sync pre + exception handlers + post
public record CovSyncPreExPost : IRequest<int>;

// ══════════════════════════════════════════════════════════════════
//  Handlers
// ══════════════════════════════════════════════════════════════════

public sealed class CovUnhandledExHandler : IRequestHandler<CovUnhandledEx, int>
{
    public ValueTask<int> Handle(CovUnhandledEx request, CancellationToken ct)
        => throw new InvalidOperationException("unhandled-boom");
}

public sealed class CovAsyncPreExHandler : IRequestHandler<CovAsyncPreEx, int>
{
    public ValueTask<int> Handle(CovAsyncPreEx request, CancellationToken ct)
        => throw new InvalidOperationException("async-pre-ex-boom");
}

public sealed class CovExOnlyFullHandler : IRequestHandler<CovExOnlyFull, int>
{
    public ValueTask<int> Handle(CovExOnlyFull request, CancellationToken ct)
        => throw new InvalidOperationException("ex-only-full-boom");
}

public sealed class CovArrayCtorHandler : IRequestHandler<CovArrayCtor, int>
{
    public ValueTask<int> Handle(CovArrayCtor request, CancellationToken ct) => new(33);
}

public sealed class CovMultiPreAsyncHandler : IRequestHandler<CovMultiPreAsync, int>
{
    public ValueTask<int> Handle(CovMultiPreAsync request, CancellationToken ct) => new(55);
}

public sealed class CovSyncPreExPostHandler : IRequestHandler<CovSyncPreExPost, int>
{
    public ValueTask<int> Handle(CovSyncPreExPost request, CancellationToken ct)
        => throw new InvalidOperationException("sync-pre-ex-post-boom");
}

// ══════════════════════════════════════════════════════════════════
//  Pipeline components
// ══════════════════════════════════════════════════════════════════

/// <summary>Exception handler that does NOT call SetHandled — triggers re-throw.</summary>
public sealed class NonHandlingExHandler<TRequest, TResponse> : IRequestExceptionHandler<TRequest, TResponse>
{
    public ValueTask Handle(TRequest request, Exception exception,
        RequestExceptionHandlerState<TResponse> state, CancellationToken ct)
        => ValueTask.CompletedTask; // intentionally does NOT call state.SetHandled()
}

/// <summary>Async exception handler that does NOT call SetHandled — triggers re-throw.</summary>
public sealed class AsyncNonHandlingExHandler<TRequest, TResponse> : IRequestExceptionHandler<TRequest, TResponse>
{
    public async ValueTask Handle(TRequest request, Exception exception,
        RequestExceptionHandlerState<TResponse> state, CancellationToken ct)
    {
        await Task.Yield();
        // intentionally does NOT call state.SetHandled()
    }
}

/// <summary>Sync pre-processor for array ctor tests.</summary>
public sealed class CovArraySyncPre<TRequest> : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken ct) => ValueTask.CompletedTask;
}

/// <summary>Sync post-processor for array ctor tests.</summary>
public sealed class CovArraySyncPost<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
        => ValueTask.CompletedTask;
}

// ══════════════════════════════════════════════════════════════════
//  Test Classes
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Gap 2: PipelineChainHandler.HandleWithExceptionHandlers — exception NOT handled → re-throw (line 241).
/// </summary>
public class UnhandledExceptionRethrowTests
{
    [Fact]
    public async Task ExceptionHandler_DoesNotHandle_ExceptionPropagates()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovUnhandledEx, int>, CovUnhandledExHandler>();
        services.AddTransient<IRequestExceptionHandler<CovUnhandledEx, int>,
            NonHandlingExHandler<CovUnhandledEx, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.Send<CovUnhandledEx, int>(new CovUnhandledEx()).AsTask());
        ex.Message.ShouldBe("unhandled-boom");
    }

    [Fact]
    public async Task AsyncExceptionHandler_DoesNotHandle_ExceptionPropagates()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovUnhandledEx, int>, CovUnhandledExHandler>();
        services.AddTransient<IRequestExceptionHandler<CovUnhandledEx, int>,
            AsyncNonHandlingExHandler<CovUnhandledEx, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.Send<CovUnhandledEx, int>(new CovUnhandledEx()).AsTask());
        ex.Message.ShouldBe("unhandled-boom");
    }
}

/// <summary>
/// Gap 3: HandleWithProcessorsAsync with exception handlers (async pre + exception handler path, lines 176-178).
/// </summary>
public class AsyncPreProcessorWithExceptionHandlerTests
{
    [Fact]
    public async Task AsyncPreProcessor_WithExceptionHandler_HandlesException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovAsyncPreEx, int>, CovAsyncPreExHandler>();
        services.AddTransient<IRequestPreProcessor<CovAsyncPreEx>, AsyncPreProcessor<CovAsyncPreEx>>();
        services.AddTransient<IRequestExceptionHandler<CovAsyncPreEx, int>,
            CovExHandler<CovAsyncPreEx, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovAsyncPreEx, int>(new CovAsyncPreEx());
        result.ShouldBe(default(int));
    }

    [Fact]
    public async Task AsyncPreProcessor_WithExceptionHandler_Unhandled_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovAsyncPreEx, int>, CovAsyncPreExHandler>();
        services.AddTransient<IRequestPreProcessor<CovAsyncPreEx>, AsyncPreProcessor<CovAsyncPreEx>>();
        services.AddTransient<IRequestExceptionHandler<CovAsyncPreEx, int>,
            NonHandlingExHandler<CovAsyncPreEx, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.Send<CovAsyncPreEx, int>(new CovAsyncPreEx()).AsTask());
    }
}

/// <summary>
/// Gap 4: HandleFull → only exception handlers (no pre/post processors), lines 125-128.
/// </summary>
public class ExceptionHandlersOnlyFullPipelineTests
{
    [Fact]
    public async Task HandleFull_ExceptionHandlersOnly_CatchesException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovExOnlyFull, int>, CovExOnlyFullHandler>();
        services.AddTransient<IRequestExceptionHandler<CovExOnlyFull, int>,
            CovExHandler<CovExOnlyFull, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovExOnlyFull, int>(new CovExOnlyFull());
        result.ShouldBe(default(int));
    }
}

/// <summary>
/// Gap 5: NotificationDispatch.TryInitialize with null.
/// </summary>
public class NotificationDispatchNullTests
{
    [Fact]
    public void TryInitialize_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => NotificationDispatch<CovSyncNotification>.TryInitialize(null!));
    }
}

/// <summary>
/// Gap 6: ServiceCollectionExtensions.AddMediator with null services.
/// </summary>
public class AddMediatorNullTests
{
    [Fact]
    public void AddMediator_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Should.Throw<ArgumentNullException>(
            () => services!.AddMediator());
    }
}

/// <summary>
/// Gap: HandleWithProcessors sync pre + exception handlers + sync post (lines 142-144).
/// Covers the path where sync pre-processors complete, then exception handlers execute.
/// </summary>
public class SyncPreWithExceptionHandlerTests
{
    [Fact]
    public async Task SyncPre_ExceptionHandler_SyncPost_FullPath()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovSyncPreExPost, int>, CovSyncPreExPostHandler>();
        services.AddTransient<IRequestPreProcessor<CovSyncPreExPost>, SyncPreProcessor<CovSyncPreExPost>>();
        services.AddTransient<IRequestExceptionHandler<CovSyncPreExPost, int>,
            CovExHandler<CovSyncPreExPost, int>>();
        services.AddTransient<IRequestPostProcessor<CovSyncPreExPost, int>,
            SyncPostProcessor<CovSyncPreExPost, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovSyncPreExPost, int>(new CovSyncPreExPost());
        result.ShouldBe(default(int));
    }
}

/// <summary>
/// Gap: HandleWithProcessorsAsync → multiple pre-processors (sync after async, lines 169-174).
/// </summary>
public class MultiplePreProcessorAsyncPathTests
{
    [Fact]
    public async Task AsyncPreProcessor_FollowedBySyncPreProcessor_BothExecute()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovMultiPreAsync, int>, CovMultiPreAsyncHandler>();
        // First pre-processor is async → triggers HandleWithProcessorsAsync
        services.AddTransient<IRequestPreProcessor<CovMultiPreAsync>, AsyncPreProcessor<CovMultiPreAsync>>();
        // Second pre-processor is sync → exercises loop at lines 169-173
        services.AddTransient<IRequestPreProcessor<CovMultiPreAsync>, SyncPreProcessor<CovMultiPreAsync>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovMultiPreAsync, int>(new CovMultiPreAsync());
        result.ShouldBe(55);
    }

    [Fact]
    public async Task MultipleAsyncPreProcessors_AllAwait()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovMultiPreAsync, int>, CovMultiPreAsyncHandler>();
        services.AddTransient<IRequestPreProcessor<CovMultiPreAsync>, AsyncPreProcessor<CovMultiPreAsync>>();
        services.AddTransient<IRequestPreProcessor<CovMultiPreAsync>, AsyncPreProcessor<CovMultiPreAsync>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovMultiPreAsync, int>(new CovMultiPreAsync());
        result.ShouldBe(55);
    }

    [Fact]
    public async Task AsyncPreProcessor_WithPostProcessor_InAsyncPath()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovMultiPreAsync, int>, CovMultiPreAsyncHandler>();
        services.AddTransient<IRequestPreProcessor<CovMultiPreAsync>, AsyncPreProcessor<CovMultiPreAsync>>();
        services.AddTransient<IRequestPostProcessor<CovMultiPreAsync, int>,
            AsyncPostProcessor<CovMultiPreAsync, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovMultiPreAsync, int>(new CovMultiPreAsync());
        result.ShouldBe(55);
    }

    [Fact]
    public async Task AsyncPreProcessor_WithSyncPostProcessor_InAsyncPath()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovMultiPreAsync, int>, CovMultiPreAsyncHandler>();
        services.AddTransient<IRequestPreProcessor<CovMultiPreAsync>, AsyncPreProcessor<CovMultiPreAsync>>();
        services.AddTransient<IRequestPostProcessor<CovMultiPreAsync, int>,
            SyncPostProcessor<CovMultiPreAsync, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovMultiPreAsync, int>(new CovMultiPreAsync());
        result.ShouldBe(55);
    }
}

/// <summary>
/// Gap: AwaitCoreAndRunPostProcessors — sync post after async core (lines 195-200).
/// </summary>
public class AwaitCoreAndRunPostProcessorsTests
{
    [Fact]
    public async Task AsyncCore_SyncPostProcessor_AwaitsCoreAndRunsPost()
    {
        // Handler that completes asynchronously + sync post-processor
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<CovAsyncPost, int>, CovAsyncPostHandler>();
        services.AddTransient<IRequestPostProcessor<CovAsyncPost, int>,
            SyncPostProcessor<CovAsyncPost, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovAsyncPost, int>(new CovAsyncPost());
        result.ShouldBe(2);
    }
}

/// <summary>
/// Gap: AwaitPostProcessorAndContinue — multiple post-processors where subsequent ones
/// are also async (lines 211-215).
/// </summary>
public class AwaitPostProcessorContinueMultipleTests
{
    [Fact]
    public async Task MultipleAsyncPostProcessors_ContinuationAwaitsAll()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovArrayCtor, int>, CovArrayCtorHandler>();
        // First async post-processor triggers AwaitPostProcessorAndContinue
        services.AddTransient<IRequestPostProcessor<CovArrayCtor, int>,
            AsyncPostProcessor<CovArrayCtor, int>>();
        // Second async post-processor exercises the loop at lines 211-215
        services.AddTransient<IRequestPostProcessor<CovArrayCtor, int>,
            AsyncPostProcessor<CovArrayCtor, int>>();
        // Third sync post-processor exercises the sync fast-path inside the continuation
        services.AddTransient<IRequestPostProcessor<CovArrayCtor, int>,
            SyncPostProcessor<CovArrayCtor, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<CovArrayCtor, int>(new CovArrayCtor());
        result.ShouldBe(33);
    }
}

/// <summary>
/// Gap: PipelineChainHandler explicit IRequestHandler interface implementation (line 249-251).
/// </summary>
public class PipelineChainHandlerExplicitInterfaceTests
{
    [Fact]
    public async Task ExplicitInterfaceHandle_RoutesToPublicHandle()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<CovArrayCtor, int>, CovArrayCtorHandler>();
        services.AddTransient<IPipelineBehavior<CovArrayCtor, int>, PassThroughBehavior<CovArrayCtor, int>>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();

        // Resolve PipelineChainHandler and call through explicit IRequestHandler interface
        var chain = sp.GetService<PipelineChainHandler<CovArrayCtor, int>>();
        chain.ShouldNotBeNull();

        IRequestHandler<CovArrayCtor, int> explicitHandler = chain;
        var result = await explicitHandler.Handle(new CovArrayCtor(), CancellationToken.None);
        result.ShouldBe(33);
    }
}

/// <summary>Pass-through behavior — does nothing, just delegates to next.</summary>
public sealed class PassThroughBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest request,
        IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
        => next.Handle(request, ct);
}
