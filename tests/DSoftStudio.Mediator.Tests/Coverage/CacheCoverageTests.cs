// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Unique types for cache tests ──

public record CovCachePing : IRequest<int>;
public record CovCacheStream : IStreamRequest<int>;
public record CovCacheBehaviorStream : IStreamRequest<int>;

// ── Dedicated type for the write-once TryInitializeHandler test ──
// Must NEVER be used anywhere else, so it is guaranteed uninitialized
// when the test runs (regardless of test order).
public record WriteOnceStream : IStreamRequest<int>;

// ── Handlers ──

public sealed class CovCachePingHandler : IRequestHandler<CovCachePing, int>
{
    public ValueTask<int> Handle(CovCachePing request, CancellationToken ct) => new(77);
}

public sealed class CovCacheStreamHandler : IStreamRequestHandler<CovCacheStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CovCacheStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return 100;
    }
}

public sealed class CovCacheBehaviorStreamHandler : IStreamRequestHandler<CovCacheBehaviorStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CovCacheBehaviorStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return 5;
    }
}

public sealed class CovCacheStreamBehavior : IStreamPipelineBehavior<CovCacheBehaviorStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CovCacheBehaviorStream request,
        IStreamRequestHandler<CovCacheBehaviorStream, int> next,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in next.Handle(request, ct))
            yield return item;
    }
}

// ── Logging behavior to create a cacheable pipeline chain ──

public sealed class CovCacheLoggingBehavior : IPipelineBehavior<CovCachePing, int>
{
    public ValueTask<int> Handle(CovCachePing request,
        IRequestHandler<CovCachePing, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}

/// <summary>
/// Tests for PipelineChainCache, StreamHandlerCache, StreamPipelineChainCache
/// and RequestDispatch/StreamDispatch edge paths.
/// </summary>
public class CacheCoverageTests
{
    [Fact]
    public async Task PipelineChainCache_Hit_ReturnsCachedChain()
    {
        // Register a Singleton behavior so the chain is cacheable (Singleton lifetime)
        var services = new ServiceCollection();
        services.AddSingleton<IPipelineBehavior<CovCachePing, int>, CovCacheLoggingBehavior>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        // First call: cache miss
        var result1 = await mediator.Send<CovCachePing, int>(new CovCachePing(), TestContext.Current.CancellationToken);
        result1.ShouldBe(77);

        // Second call on same thread/scope: cache hit
        var result2 = await mediator.Send<CovCachePing, int>(new CovCachePing(), TestContext.Current.CancellationToken);
        result2.ShouldBe(77);
    }

    [Fact]
    public async Task StreamHandlerCache_Hit_ReturnsCachedHandler()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        // First call
        var items1 = new List<int>();
        await foreach (var item in mediator.CreateStream<CovCacheStream, int>(new CovCacheStream(), TestContext.Current.CancellationToken))
            items1.Add(item);
        items1.ShouldBe(new[] { 100 });

        // Second call on same scope (cache hit)
        var items2 = new List<int>();
        await foreach (var item in mediator.CreateStream<CovCacheStream, int>(new CovCacheStream(), TestContext.Current.CancellationToken))
            items2.Add(item);
        items2.ShouldBe(new[] { 100 });
    }

    [Fact]
    public async Task StreamPipelineChainCache_WithBehavior_CachesChain()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamPipelineBehavior<CovCacheBehaviorStream, int>, CovCacheStreamBehavior>();
        services.AddMediator().RegisterMediatorHandlers()
            .PrecompilePipelines().PrecompileNotifications().PrecompileStreams();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        // First call: cache miss
        var items1 = new List<int>();
        await foreach (var item in mediator.CreateStream<CovCacheBehaviorStream, int>(new CovCacheBehaviorStream(), TestContext.Current.CancellationToken))
            items1.Add(item);
        items1.ShouldBe(new[] { 5 });

        // Second call: cache hit
        var items2 = new List<int>();
        await foreach (var item in mediator.CreateStream<CovCacheBehaviorStream, int>(new CovCacheBehaviorStream(), TestContext.Current.CancellationToken))
            items2.Add(item);
        items2.ShouldBe(new[] { 5 });
    }

    [Fact]
    public void StreamDispatch_TryInitializeHandler_WriteOnce_FirstTrueSecondFalse()
    {
        // WriteOnceStream is dedicated to this test — guaranteed uninitialized.
        var first = StreamDispatch<WriteOnceStream, int>.TryInitializeHandler(
            static sp => null!);
        first.ShouldBeTrue();

        var second = StreamDispatch<WriteOnceStream, int>.TryInitializeHandler(
            static sp => null!);
        second.ShouldBeFalse();
    }

    [Fact]
    public void StreamDispatch_TryInitializeHandler_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => StreamDispatch<CovCacheStream, int>.TryInitializeHandler(null!));
    }

    [Fact]
    public void StreamDispatch_TryInitializePipeline_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => StreamDispatch<CovCacheStream, int>.TryInitializePipeline(null!));
    }
}
