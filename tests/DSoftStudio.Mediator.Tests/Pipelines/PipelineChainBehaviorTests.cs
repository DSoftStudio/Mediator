// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// ── Unique request types per scenario ──────────────────────────────

public sealed record BehaviorSyncPing : IRequest<int>;
public sealed record BehaviorAsyncPing : IRequest<int>;
public sealed record MultiBehaviorPing : IRequest<int>;

// ── Handlers ───────────────────────────────────────────────────────

public sealed class BehaviorSyncPingHandler : IRequestHandler<BehaviorSyncPing, int>
{
    public ValueTask<int> Handle(BehaviorSyncPing request, CancellationToken ct)
        => new(42);
}

public sealed class BehaviorAsyncPingHandler : IRequestHandler<BehaviorAsyncPing, int>
{
    public async ValueTask<int> Handle(BehaviorAsyncPing request, CancellationToken ct)
    {
        await Task.Yield();
        return 99;
    }
}

public sealed class MultiBehaviorPingHandler : IRequestHandler<MultiBehaviorPing, int>
{
    public ValueTask<int> Handle(MultiBehaviorPing request, CancellationToken ct)
        => new(1);
}

// ── Behaviors ──────────────────────────────────────────────────────

public sealed class SyncTrackBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public SyncTrackBehavior(List<string> log) => _log = log;

    public ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add("sync:before");
        var result = next.Handle(request, ct);
        _log.Add("sync:after");
        return result;
    }
}

public sealed class AsyncTrackBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public AsyncTrackBehavior(List<string> log) => _log = log;

    public async ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken ct)
    {
        _log.Add("async:before");
        await Task.Yield();
        var result = await next.Handle(request, ct);
        _log.Add("async:after");
        return result;
    }
}

// ── Tests ───────────────────────────────────────────────────────────

public class PipelineChainBehaviorTests
{
    [Fact]
    public async Task Send_WithSyncBehavior_SyncHandler_ExecutesFastPath()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient<IPipelineBehavior<BehaviorSyncPing, int>>(_ => new SyncTrackBehavior<BehaviorSyncPing, int>(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new BehaviorSyncPing());

        result.ShouldBe(42);
        log.ShouldBe(new[] {"sync:before", "sync:after"});
    }

    [Fact]
    public async Task Send_WithAsyncBehavior_HitsAwaitAndReleasePath()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient<IPipelineBehavior<BehaviorSyncPing, int>>(_ => new AsyncTrackBehavior<BehaviorSyncPing, int>(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new BehaviorSyncPing());

        result.ShouldBe(42);
        log.ShouldBe(new[] {"async:before", "async:after"});
    }

    [Fact]
    public async Task Send_WithAsyncBehavior_AsyncHandler_FullAsyncPath()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient<IPipelineBehavior<BehaviorAsyncPing, int>>(_ => new AsyncTrackBehavior<BehaviorAsyncPing, int>(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new BehaviorAsyncPing());

        result.ShouldBe(99);
        log.ShouldBe(new[] {"async:before", "async:after"});
    }

    [Fact]
    public async Task Send_WithMultipleBehaviors_ChainExecutesInOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient<IPipelineBehavior<MultiBehaviorPing, int>>(_ => new SyncTrackBehavior<MultiBehaviorPing, int>(log));
        services.AddTransient<IPipelineBehavior<MultiBehaviorPing, int>>(_ => new AsyncTrackBehavior<MultiBehaviorPing, int>(log));
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MultiBehaviorPing());

        result.ShouldBe(1);
        // "before" entries are deterministic: sync wraps async, so sync enters first.
        // "after" entries race because SyncTrackBehavior does not await the inner
        // ValueTask while AsyncTrackBehavior resumes on a thread-pool thread.
        log.Count.ShouldBe(4);
        log[0].ShouldBe("sync:before");
        log[1].ShouldBe("async:before");
        log.Skip(2).Order().ShouldBe(["async:after", "sync:after"]);
    }
}
