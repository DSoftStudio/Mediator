// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Unique request types ───────────────────────────────────────────

public sealed record ProcessorSyncPing : IRequest<int>;
public sealed record ProcessorAsyncPing : IRequest<int>;
public sealed record ProcessorWithExPing(bool ShouldThrow) : IRequest<int>;

// ── Handlers ───────────────────────────────────────────────────────

public sealed class ProcessorSyncPingHandler : IRequestHandler<ProcessorSyncPing, int>
{
    public ValueTask<int> Handle(ProcessorSyncPing request, CancellationToken ct) => new(42);
}

public sealed class ProcessorAsyncPingHandler : IRequestHandler<ProcessorAsyncPing, int>
{
    public async ValueTask<int> Handle(ProcessorAsyncPing request, CancellationToken ct)
    {
        await Task.Yield();
        return 99;
    }
}

public sealed class ProcessorWithExPingHandler : IRequestHandler<ProcessorWithExPing, int>
{
    public ValueTask<int> Handle(ProcessorWithExPing request, CancellationToken ct)
    {
        if (request.ShouldThrow) throw new InvalidOperationException("boom");
        return new(42);
    }
}

// ── Sync Pre/Post processors ───────────────────────────────────────

public sealed class SyncPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public List<string> Log { get; } = new();
    public ValueTask Process(TRequest request, CancellationToken ct)
    {
        Log.Add("pre:sync");
        return ValueTask.CompletedTask;
    }
}

public sealed class SyncPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public List<string> Log { get; } = new();
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        Log.Add($"post:sync:{response}");
        return ValueTask.CompletedTask;
    }
}

// ── Async Pre/Post processors ──────────────────────────────────────

public sealed class AsyncPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public List<string> Log { get; } = new();
    public async ValueTask Process(TRequest request, CancellationToken ct)
    {
        await Task.Yield();
        Log.Add("pre:async");
    }
}

public sealed class AsyncPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public List<string> Log { get; } = new();
    public async ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        await Task.Yield();
        Log.Add($"post:async:{response}");
    }
}

// ── Tests ───────────────────────────────────────────────────────────

public class PipelineProcessorAsyncTests
{
    [Fact]
    public async Task Send_SyncPreProcessor_SyncHandler_SyncPostProcessor()
    {
        var pre = new SyncPreProcessor<ProcessorSyncPing>();
        var post = new SyncPostProcessor<ProcessorSyncPing, int>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<ProcessorSyncPing>>(pre);
        services.AddSingleton<IRequestPostProcessor<ProcessorSyncPing, int>>(post);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ProcessorSyncPing());

        result.ShouldBe(42);
        pre.Log.ShouldBe(new[] {"pre:sync"});
        post.Log.ShouldBe(new[] {"post:sync:42"});
    }

    [Fact]
    public async Task Send_AsyncPreProcessor_HitsHandleWithProcessorsAsyncPath()
    {
        var pre = new AsyncPreProcessor<ProcessorSyncPing>();
        var post = new SyncPostProcessor<ProcessorSyncPing, int>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<ProcessorSyncPing>>(pre);
        services.AddSingleton<IRequestPostProcessor<ProcessorSyncPing, int>>(post);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ProcessorSyncPing());

        result.ShouldBe(42);
        pre.Log.ShouldBe(new[] {"pre:async"});
        post.Log.ShouldBe(new[] {"post:sync:42"});
    }

    [Fact]
    public async Task Send_SyncPreProcessor_AsyncHandler_HitsAwaitCoreAndRunPostProcessors()
    {
        var pre = new SyncPreProcessor<ProcessorAsyncPing>();
        var post = new SyncPostProcessor<ProcessorAsyncPing, int>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<ProcessorAsyncPing>>(pre);
        services.AddSingleton<IRequestPostProcessor<ProcessorAsyncPing, int>>(post);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ProcessorAsyncPing());

        result.ShouldBe(99);
        pre.Log.ShouldBe(new[] {"pre:sync"});
        post.Log.ShouldBe(new[] {"post:sync:99"});
    }

    [Fact]
    public async Task Send_AsyncPostProcessor_HitsAwaitPostProcessorAndContinue()
    {
        var pre = new SyncPreProcessor<ProcessorSyncPing>();
        var post = new AsyncPostProcessor<ProcessorSyncPing, int>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<ProcessorSyncPing>>(pre);
        services.AddSingleton<IRequestPostProcessor<ProcessorSyncPing, int>>(post);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ProcessorSyncPing());

        result.ShouldBe(42);
        pre.Log.ShouldBe(new[] {"pre:sync"});
        post.Log.ShouldBe(new[] {"post:async:42"});
    }

    [Fact]
    public async Task Send_MultipleAsyncPreProcessors_AllExecute()
    {
        var pre1 = new AsyncPreProcessor<ProcessorSyncPing>();
        var pre2 = new AsyncPreProcessor<ProcessorSyncPing>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<ProcessorSyncPing>>(pre1);
        services.AddSingleton<IRequestPreProcessor<ProcessorSyncPing>>(pre2);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ProcessorSyncPing());

        result.ShouldBe(42);
        pre1.Log.ShouldBe(new[] {"pre:async"});
        pre2.Log.ShouldBe(new[] {"pre:async"});
    }

    [Fact]
    public async Task Send_PostProcessorsOnly_NoPreProcessor()
    {
        var post = new SyncPostProcessor<ProcessorSyncPing, int>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPostProcessor<ProcessorSyncPing, int>>(post);
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(new ProcessorSyncPing());

        result.ShouldBe(42);
        post.Log.ShouldBe(new[] {"post:sync:42"});
    }

    [Fact]
    public async Task Send_ProcessorsWithExceptionHandlers_BothActive()
    {
        var pre = new SyncPreProcessor<ProcessorWithExPing>();
        var post = new SyncPostProcessor<ProcessorWithExPing, int>();
        var exHandler = new FallbackExceptionHandler();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<ProcessorWithExPing>>(pre);
        services.AddSingleton<IRequestPostProcessor<ProcessorWithExPing, int>>(post);
        // Use a generic exception handler for this type
        services.AddSingleton<IRequestExceptionHandler<ProcessorWithExPing, int>>(
            new ProcessorExceptionHandler());
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IMediator>().Send(
            new ProcessorWithExPing(true));

        result.ShouldBe(-1);
        pre.Log.ShouldBe(new[] {"pre:sync"});
    }
}

// ── Helper for the combined processors+exception test ──────────────

public sealed class ProcessorExceptionHandler : IRequestExceptionHandler<ProcessorWithExPing, int>
{
    public ValueTask Handle(ProcessorWithExPing request, Exception exception, RequestExceptionHandlerState<int> state, CancellationToken ct)
    {
        state.SetHandled(-1);
        return ValueTask.CompletedTask;
    }
}
