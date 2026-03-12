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
    /// Only used when <see cref="StreamDispatch{TRequest, TResponse}.IsStreamChainCacheable"/>
    /// is <see langword="true"/> (Scoped or Singleton lifetime). Transient chains are never cached.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StreamPipelineChainCache<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        [ThreadStatic]
        private static StreamPipelineChainHandler<TRequest, TResponse>? _cachedChain;

        /// <summary>
        /// Returns the stream pipeline chain for the given service provider, using the
        /// thread-local cache when the provider matches. Returns <see langword="null"/> if
        /// no chain is registered (no-behaviors path). Cost: ~1 ns (cache hit) vs ~10 ns (cache miss).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StreamPipelineChainHandler<TRequest, TResponse>? Resolve(IServiceProvider serviceProvider)
        {
            if (ReferenceEquals(_cachedProvider, serviceProvider))
                return _cachedChain;

            var chain = serviceProvider.GetService<StreamPipelineChainHandler<TRequest, TResponse>>();
            _cachedProvider = serviceProvider;
            _cachedChain = chain;
            return chain;
        }
    }
}
