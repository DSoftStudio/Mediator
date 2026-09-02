// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-thread cache for <see cref="StreamPipelineChainHandler{TRequest, TResponse}"/> on the
    /// stream hot path.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> to store the last-resolved stream pipeline chain per thread.
    /// </para>
    /// <para>
    /// <b>Cacheability is decided here, not by the caller.</b> A Transient chain must never be
    /// cached, and that used to be a
    /// <see cref="StreamDispatch{TRequest, TResponse}.IsStreamChainCacheable"/> read plus a ternary
    /// in the emitted dispatch body — duplicated across every stream dispatch body, exactly as it
    /// was on the Send side (see <see cref="PipelineChainCache{TRequest, TResponse}"/>). It is one
    /// fact about the pair, settled at registration; it now lives on the miss path, which is cold.
    /// </para>
    /// <para>
    /// <b>And it is asked of the CONTAINER, not of a static.</b>
    /// <see cref="StreamDispatch{TRequest, TResponse}.IsStreamChainCacheable"/> is process-global and
    /// monotonic while a chain lifetime is per container, so the first container to register a Scoped
    /// or Singleton stream chain latched it on for every container after it. See
    /// <see cref="PipelineChainCache{TRequest, TResponse}"/> for the full account.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StreamPipelineChainCache<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private static readonly Type ServiceType = typeof(StreamPipelineChainHandler<TRequest, TResponse>);

        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        [ThreadStatic]
        private static StreamPipelineChainHandler<TRequest, TResponse>? _cachedChain;

        /// <summary>
        /// Returns the stream pipeline chain for the given service provider, from the thread-local
        /// cache when this pair may be cached and the cache holds one for this provider. Returns
        /// <see langword="null"/> when no chain is registered (no-behaviors path).
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
        public static StreamPipelineChainHandler<TRequest, TResponse>? Resolve(IServiceProvider serviceProvider)
        {
            // The provider must be non-null for the cache to be meaningful: ReferenceEquals(null, null)
            // is true, so a null provider would hit a "cached" null chain and fail somewhere else.
            if (serviceProvider is not null && ReferenceEquals(_cachedProvider, serviceProvider))
            {
                var cached = _cachedChain;
                if (cached is not null)
                    return cached;

                return serviceProvider.GetService<StreamPipelineChainHandler<TRequest, TResponse>>();
            }

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static StreamPipelineChainHandler<TRequest, TResponse>? ResolveSlow(IServiceProvider serviceProvider)
        {
            var chain = serviceProvider.GetService<StreamPipelineChainHandler<TRequest, TResponse>>();

            _cachedProvider = serviceProvider;
            _cachedChain = DispatchCacheability.AllowsCaching(serviceProvider, ServiceType)
                ? chain
                : null;

            return chain;
        }
    }
}
