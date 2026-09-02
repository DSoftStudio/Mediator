// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Coverage;

public sealed record AbsentPing : IRequest<int>;

public sealed class AbsentPingHandler : IRequestHandler<AbsentPing, int>
{
    public ValueTask<int> Handle(AbsentPing request, CancellationToken ct) => new(1);
}

public sealed record AbsentStream : IStreamRequest<int>;

public sealed class AbsentStreamHandler : IStreamRequestHandler<AbsentStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        AbsentStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield return 1;
    }
}

/// <summary>
/// A container that has NO pipeline chain for a pair must be asked once, not on every dispatch.
/// <para>
/// The slot's "provider matches, value null" state meant two different things — "registered Transient
/// here", which has to resolve fresh, and "absent here", which can only ever resolve to null. The
/// second fell through to a <c>GetService</c> on every dispatch, permanently. On the stream path
/// nothing gated it at all, so every <c>CreateStream</c> of a pair with no stream behaviors — the
/// common case — paid it; on the request path it hit any container that did not register the chain
/// its process-global sibling had latched on.
/// </para>
/// </summary>
public class AbsentChainNotReProbedTests
{
    private sealed class CountingProvider(IServiceProvider inner) : IServiceProvider
    {
        public int ChainProbes;

        public object? GetService(Type serviceType)
        {
            if (serviceType.IsGenericType)
            {
                var definition = serviceType.GetGenericTypeDefinition();
                if (definition == typeof(PipelineChainHandler<,>) || definition == typeof(StreamPipelineChainHandler<,>))
                    Interlocked.Increment(ref ChainProbes);
            }

            return inner.GetService(serviceType);
        }
    }

    private static ServiceProvider BuildWithoutChains()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.PrecompilePipelines().PrecompileStreams();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void RequestChain_AbsentHere_IsAskedOnce()
    {
        using var provider = BuildWithoutChains();
        var counting = new CountingProvider(provider);

        for (int i = 0; i < 10; i++)
            PipelineChainCache<AbsentPing, int>.Resolve(counting).ShouldBeNull();

        // One cold miss. Before the absent state existed this read 10.
        counting.ChainProbes.ShouldBe(1);

        DispatchCacheReleaser.ReleaseFor(counting);
    }

    [Fact]
    public void StreamChain_AbsentHere_IsAskedOnce()
    {
        using var provider = BuildWithoutChains();
        var counting = new CountingProvider(provider);

        for (int i = 0; i < 10; i++)
            StreamPipelineChainCache<AbsentStream, int>.Resolve(counting).ShouldBeNull();

        counting.ChainProbes.ShouldBe(1);

        DispatchCacheReleaser.ReleaseFor(counting);
    }

    [Fact]
    public void AnAbsentVerdictDoesNotLeakToAContainerThatHasAChain()
    {
        using (var without = BuildWithoutChains())
        {
            PipelineChainCache<AbsentPing, int>.Resolve(without).ShouldBeNull();
        }

        // A different container, this one WITH a chain for the same pair. The slot is reused; the
        // verdict must not be.
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddTransient<IPipelineBehavior<AbsentPing, int>, AbsentPassThroughBehavior>();
        services.PrecompilePipelines();

        using var with = services.BuildServiceProvider();

        PipelineChainCache<AbsentPing, int>.Resolve(with).ShouldNotBeNull();
    }
}

file sealed class AbsentPassThroughBehavior : IPipelineBehavior<AbsentPing, int>
{
    public ValueTask<int> Handle(AbsentPing request, IRequestHandler<AbsentPing, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}
