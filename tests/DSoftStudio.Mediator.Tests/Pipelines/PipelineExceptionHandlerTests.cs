// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Unique request types ───────────────────────────────────────────

public sealed record ExHandlerPing(bool ShouldThrow) : IRequest<int>;

// ── Handler that can throw ─────────────────────────────────────────

public sealed class ExHandlerPingHandler : IRequestHandler<ExHandlerPing, int>
{
    public ValueTask<int> Handle(ExHandlerPing request, CancellationToken ct)
    {
        if (request.ShouldThrow)
            throw new InvalidOperationException("handler failed");
        return new(42);
    }
}

// ── Exception handler that handles the error ───────────────────────

public sealed class FallbackExceptionHandler : IRequestExceptionHandler<ExHandlerPing, int>
{
    public int CallCount;

    public ValueTask Handle(ExHandlerPing request, Exception exception, RequestExceptionHandlerState<int> state, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        state.SetHandled(-1);
        return ValueTask.CompletedTask;
    }
}

// ── Exception handler that does NOT handle (passes through) ────────

public sealed class LoggingExceptionHandler : IRequestExceptionHandler<ExHandlerPing, int>
{
    public int CallCount;

    public ValueTask Handle(ExHandlerPing request, Exception exception, RequestExceptionHandlerState<int> state, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        // Does NOT call state.SetHandled — exception propagates
        return ValueTask.CompletedTask;
    }
}

// ── Async exception handler ────────────────────────────────────────

public sealed class AsyncFallbackExceptionHandler : IRequestExceptionHandler<ExHandlerPing, int>
{
    public int CallCount;

    public async ValueTask Handle(ExHandlerPing request, Exception exception, RequestExceptionHandlerState<int> state, CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref CallCount);
        state.SetHandled(-99);
    }
}

// ── Tests ───────────────────────────────────────────────────────────

public class PipelineExceptionHandlerTests
{
    [Fact]
    public async Task Send_NoException_ExceptionHandlerNotInvoked()
    {
        var handler = new FallbackExceptionHandler();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestExceptionHandler<ExHandlerPing, int>>(handler);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ExHandlerPing(false));

        result.ShouldBe(42);
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Send_Exception_HandledByExceptionHandler_ReturnsFallback()
    {
        var handler = new FallbackExceptionHandler();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestExceptionHandler<ExHandlerPing, int>>(handler);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ExHandlerPing(true));

        result.ShouldBe(-1);
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Send_Exception_NotHandled_Propagates()
    {
        var logger = new LoggingExceptionHandler();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestExceptionHandler<ExHandlerPing, int>>(logger);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        Func<Task> act = () => mediator.Send(new ExHandlerPing(true)).AsTask();

        await Should.ThrowAsync<InvalidOperationException>(act);
        logger.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Send_Exception_MultipleHandlers_FirstHandlesStopsChain()
    {
        var first = new FallbackExceptionHandler();
        var second = new LoggingExceptionHandler();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestExceptionHandler<ExHandlerPing, int>>(first);
        services.AddSingleton<IRequestExceptionHandler<ExHandlerPing, int>>(second);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ExHandlerPing(true));

        result.ShouldBe(-1);
        first.CallCount.ShouldBe(1);
        second.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Send_Exception_AsyncExceptionHandler_HandlesCorrectly()
    {
        var handler = new AsyncFallbackExceptionHandler();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestExceptionHandler<ExHandlerPing, int>>(handler);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new ExHandlerPing(true));

        result.ShouldBe(-99);
        handler.CallCount.ShouldBe(1);
    }
}
