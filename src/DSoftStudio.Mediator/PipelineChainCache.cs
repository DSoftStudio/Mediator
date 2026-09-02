// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Resolves the <see cref="PipelineChainHandler{TRequest, TResponse}"/> for a dispatch, caching it
    /// per (thread, provider) when the chain's registered lifetime allows it.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> to hold the last-resolved chain per thread. When the caller's
    /// <see cref="IServiceProvider"/> matches the cached one — the same DI scope — the cached chain is
    /// returned directly, avoiding the ~10 ns <c>GetService</c> lookup on every subsequent
    /// <c>Send</c>.
    /// </para>
    /// <para>
    /// <b>Cacheability is decided here, not by the caller.</b> A Transient chain must never be cached,
    /// and that used to be a ternary in the emitted dispatch body — duplicated across all FOUR Send
    /// bodies. It is one fact about the pair; it belongs on the miss path, which is cold.
    /// </para>
    /// <para>
    /// <b>And it is asked of the CONTAINER, not of a static.</b>
    /// <see cref="RequestDispatch{TRequest, TResponse}.IsPipelineChainCacheable"/> is one static per
    /// closed generic pair, so it is process-global and monotonic, while a chain's lifetime is decided
    /// per container. Two containers disagree routinely — a test suite, a modular monolith, a host that
    /// rebuilds its provider — and the first one to register a Scoped or Singleton chain latched that
    /// flag on for every container that followed, so a later container's genuinely Transient chain was
    /// cached and pinned. <see cref="DispatchCacheability"/> answers per container instead. The static
    /// flag remains for compatibility but no longer decides caching.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class PipelineChainCache<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly Type ServiceType = typeof(PipelineChainHandler<TRequest, TResponse>);

        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        // Tri-state, read together with _cachedProvider:
        //   provider matches + chain non-null -> reusable, this is the chain
        //   provider matches + chain null     -> Transient here, or this container has no chain for
        //                                        the pair: resolve fresh, do not re-ask the container
        //   provider differs                  -> cold: ask the container and remember the verdict
        [ThreadStatic]
        private static PipelineChainHandler<TRequest, TResponse>? _cachedChain;

        /// <summary>
        /// Returns the pipeline chain for the given provider, from the thread-local cache when the
        /// provider matches and its registration allows reuse. Cost: ~1 ns on a hit, ~10 ns otherwise.
        /// Returns <see langword="null"/> when this container registers no chain for the pair.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PipelineChainHandler<TRequest, TResponse>? Resolve(IServiceProvider serviceProvider)
        {
            // The provider must be non-null for the cache to be meaningful: ReferenceEquals(null, null)
            // is true, so a null provider would hit a "cached" null chain and fail somewhere else.
            if (serviceProvider is not null && ReferenceEquals(_cachedProvider, serviceProvider))
            {
                var cached = _cachedChain;
                if (cached is not null)
                    return cached;

                return serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();
            }

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PipelineChainHandler<TRequest, TResponse>? ResolveSlow(IServiceProvider serviceProvider)
        {
            var chain = serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();

            _cachedProvider = serviceProvider;
            _cachedChain = DispatchCacheability.AllowsCaching(serviceProvider, ServiceType)
                ? chain
                : null;

            return chain;
        }
    }
}
