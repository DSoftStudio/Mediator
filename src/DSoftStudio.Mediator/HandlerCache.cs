// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-thread cache for <see cref="IRequestHandler{TRequest, TResponse}"/> on the
    /// no-behaviors hot path.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> to store the last-resolved handler per thread.
    /// When the caller's <see cref="IServiceProvider"/> matches the cached provider
    /// (same DI scope), the cached handler is returned directly — eliminating the
    /// ~10 ns <c>GetRequiredService</c> lookup on every subsequent <c>Send</c> call.
    /// </para>
    /// <para>
    /// On scope change (new request in ASP.NET Core) the <c>ReferenceEquals</c> guard
    /// detects the mismatch and falls back to a normal DI resolution.
    /// After an <c>async/await</c> thread hop the cache simply misses — no correctness
    /// issue, just one DI lookup on the new thread.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HandlerCache<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        [ThreadStatic]
        private static IRequestHandler<TRequest, TResponse>? _cachedHandler;

        /// <summary>
        /// Returns the handler for the given service provider, using the thread-local
        /// cache when the provider matches. Cost: ~1 ns (cache hit) vs ~10 ns (cache miss).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IRequestHandler<TRequest, TResponse> Resolve(IServiceProvider serviceProvider)
        {
            if (ReferenceEquals(_cachedProvider, serviceProvider))
                return _cachedHandler!;

            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            _cachedProvider = serviceProvider;
            _cachedHandler = handler;
            return handler;
        }
    }
}
