// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

// ── Pairs owned outright by this file ────────────────────────────────────────
// The dispatch flags are static per (TRequest, TResponse) and PROCESS-GLOBAL, and
// PrecompilePipelines() runs RegisterPipeline for every pair the generator discovered — against
// whichever ServiceCollection the CALLING test built. So an ordinary handler type here would let a
// sibling test class that registers a dispatch observer latch our pair's cacheable flag on from its
// own container, and the assertions below would fail depending on test order.
//
// `file` types are the one shape the generator skips at all three discovery points
// (HandlerDiscovery.IsFileLocal), because generated code cannot name them. No registration is ever
// emitted for these pairs, so their flags are ours alone and we set them by hand.

file record UncacheablePing : IRequest<int>;

file record CacheablePing : IRequest<int>;

file record UncacheableStream : IStreamRequest<int>;

file sealed class UncacheablePingHandler : IRequestHandler<UncacheablePing, int>
{
    public ValueTask<int> Handle(UncacheablePing request, CancellationToken ct) => new(7);
}

file sealed class CacheablePingHandler : IRequestHandler<CacheablePing, int>
{
    public ValueTask<int> Handle(CacheablePing request, CancellationToken ct) => new(7);
}

file sealed class UncacheableStreamHandler : IStreamRequestHandler<UncacheableStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        UncacheableStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield return 7;
    }
}

/// <summary>
/// A pipeline chain registered Transient must be resolved fresh on every dispatch and never served
/// from the per-(thread, provider) cache.
/// <para>
/// This invariant used to be an <c>IsPipelineChainCacheable</c> ternary emitted into each of the four
/// Send dispatch bodies (and each stream body); it now lives inside
/// <see cref="PipelineChainCache{TRequest, TResponse}.Resolve"/> and
/// <see cref="StreamPipelineChainCache{TRequest, TResponse}.Resolve"/>. Delete either check and the
/// "fresh instance" tests below fail.
/// </para>
/// </summary>
public class TransientChainCachingTests
{
    /// <summary>Transient chain registration: DI hands back a distinct instance on every resolve,
    /// which is what makes a wrongly-cached chain observable by reference.</summary>
    private static ServiceProvider BuildTransientChainProvider<TRequest, THandler>()
        where TRequest : IRequest<int>
        where THandler : class, IRequestHandler<TRequest, int>
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<TRequest, int>, THandler>();
        services.AddTransient<PipelineChainHandler<TRequest, int>>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolve_UncacheableChain_ReturnsFreshInstancePerCall()
    {
        RequestDispatch<UncacheablePing, int>.IsPipelineChainCacheable.ShouldBeFalse();

        using var sp = BuildTransientChainProvider<UncacheablePing, UncacheablePingHandler>();

        var first = PipelineChainCache<UncacheablePing, int>.Resolve(sp);
        var second = PipelineChainCache<UncacheablePing, int>.Resolve(sp);
        var third = PipelineChainCache<UncacheablePing, int>.Resolve(sp);

        first.ShouldNotBeNull();
        second.ShouldNotBeSameAs(first);
        third.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void Resolve_CacheableChain_ReturnsTheSameInstanceForOneProvider()
    {
        // The positive half: without it, a Resolve that never cached anything would also pass the
        // "fresh instance" test above.
        RequestDispatch<CacheablePing, int>.MarkPipelineChainCacheable();

        using var sp = BuildTransientChainProvider<CacheablePing, CacheablePingHandler>();

        var first = PipelineChainCache<CacheablePing, int>.Resolve(sp);
        var second = PipelineChainCache<CacheablePing, int>.Resolve(sp);

        first.ShouldNotBeNull();
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void StreamResolve_UncacheableChain_ReturnsFreshInstancePerCall()
    {
        StreamDispatch<UncacheableStream, int>.IsStreamChainCacheable.ShouldBeFalse();

        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<UncacheableStream, int>, UncacheableStreamHandler>();
        services.AddTransient<StreamPipelineChainHandler<UncacheableStream, int>>();
        using var sp = services.BuildServiceProvider();

        var first = StreamPipelineChainCache<UncacheableStream, int>.Resolve(sp);
        var second = StreamPipelineChainCache<UncacheableStream, int>.Resolve(sp);

        first.ShouldNotBeNull();
        second.ShouldNotBeSameAs(first);
    }
}
