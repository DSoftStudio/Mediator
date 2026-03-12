// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-thread cache for <see cref="PipelineChainHandler{TRequest, TResponse}"/> on the
    /// behaviors hot path.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> to store the last-resolved pipeline chain per thread.
    /// When the caller's <see cref="IServiceProvider"/> matches the cached provider
    /// (same DI scope), the cached chain is returned directly — eliminating the
    /// ~10 ns <c>GetService</c> lookup on every subsequent <c>Send</c> call.
    /// </para>
    /// <para>
    /// Only used when <see cref="RequestDispatch{TRequest, TResponse}.IsPipelineChainCacheable"/>
    /// is <see langword="true"/> (Scoped or Singleton lifetime). Transient chains are never cached.
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
        /// Returns the pipeline chain for the given service provider, using the thread-local
        /// cache when the provider matches. Cost: ~1 ns (cache hit) vs ~10 ns (cache miss).
        /// Returns <see langword="null"/> if no chain is registered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PipelineChainHandler<TRequest, TResponse>? Resolve(IServiceProvider serviceProvider)
        {
            if (ReferenceEquals(_cachedProvider, serviceProvider))
                return _cachedChain;

            var chain = serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();
            _cachedProvider = serviceProvider;
            _cachedChain = chain;
            return chain;
        }
    }
}
