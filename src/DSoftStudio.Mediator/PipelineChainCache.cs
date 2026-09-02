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
    /// per (thread, provider) when the chain's lifetime allows.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> to hold the last-resolved chain per thread. When the caller's
    /// <see cref="IServiceProvider"/> matches the cached one — the same DI scope — the cached chain is
    /// returned directly, avoiding the ~10 ns <c>GetService</c> lookup on every subsequent
    /// <c>Send</c>.
    /// </para>
    /// <para>
    /// <b>Cacheability is decided here, not by the caller.</b> A Transient chain must never be cached,
    /// and that used to be a <see cref="RequestDispatch{TRequest, TResponse}.IsPipelineChainCacheable"/>
    /// read plus a ternary in the emitted dispatch body — duplicated across all FOUR Send bodies (the
    /// shared interceptor/extension emitter, the <c>Send(object)</c> switch, the
    /// <c>RequestObjectDispatch</c> delegate, and <see cref="Mediator"/>'s virtual fallback). It is one
    /// fact about the pair, settled at registration; reading it on every dispatch, in four places,
    /// bought nothing. It now lives on the miss path, which is cold.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class PipelineChainCache<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        [ThreadStatic]
        private static PipelineChainHandler<TRequest, TResponse>? _cachedChain;

        /// <summary>
        /// Returns the pipeline chain for the given provider, from the thread-local cache when this
        /// pair may be cached and the cache holds one for this provider. Cost: ~1 ns on a hit, ~10 ns
        /// otherwise. Returns <see langword="null"/> when no chain is registered for this pair.
        /// </summary>
        /// <para>
        /// The cacheability check comes FIRST, before the thread-local probe, and that order is
        /// deliberate. A Transient chain never populates the cache, so probing it first would cost that
        /// pair a <c>[ThreadStatic]</c> read and a call frame that can only ever miss — and Transient is
        /// the default lifetime for <c>MediatorBuilder.AddBehavior</c>. This ordering leaves BOTH paths
        /// with exactly the instruction sequence they had when the decision was emitted into each
        /// dispatch body; the win is that it is now written once instead of four times.
        /// </para>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PipelineChainHandler<TRequest, TResponse>? Resolve(IServiceProvider serviceProvider)
        {
            // Transient chains get a fresh instance per resolve, so caching one would pin the first and
            // hand it to every later dispatch on this thread. Resolve it and hand it back uncached.
            if (!RequestDispatch<TRequest, TResponse>.IsPipelineChainCacheable)
                return serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();

            // The provider must be non-null for the cache to be meaningful: ReferenceEquals(null, null)
            // is true, so a null provider would hit a "cached" null chain and fail somewhere else.
            if (serviceProvider is not null && ReferenceEquals(_cachedProvider, serviceProvider))
                return _cachedChain;

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PipelineChainHandler<TRequest, TResponse>? ResolveSlow(IServiceProvider serviceProvider)
        {
            var chain = serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();
            _cachedProvider = serviceProvider;
            _cachedChain = chain;
            return chain;
        }
    }
}
