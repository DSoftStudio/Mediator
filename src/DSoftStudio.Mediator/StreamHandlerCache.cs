// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-thread cache for <see cref="IStreamRequestHandler{TRequest, TResponse}"/> on the
    /// no-behaviors stream hot path.
    /// <para>
    /// Uses the <see cref="StreamDispatch{TRequest, TResponse}.Handler"/> factory on first access,
    /// then caches the result per thread. The <see cref="IServiceProvider"/> reference equality
    /// guard detects scope changes and re-resolves automatically.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StreamHandlerCache<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        [ThreadStatic]
        private static IStreamRequestHandler<TRequest, TResponse>? _cachedHandler;

        /// <summary>
        /// Returns the stream handler for the given service provider, using the thread-local
        /// cache when the provider matches. Cost: ~1 ns (cache hit) vs ~10 ns (cache miss).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamRequestHandler<TRequest, TResponse> Resolve(IServiceProvider serviceProvider)
        {
            if (ReferenceEquals(_cachedProvider, serviceProvider))
                return _cachedHandler!;

            var handler = StreamDispatch<TRequest, TResponse>.Handler!(serviceProvider);
            _cachedProvider = serviceProvider;
            _cachedHandler = handler;
            return handler;
        }
    }
}
