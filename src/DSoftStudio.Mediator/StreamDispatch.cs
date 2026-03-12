// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Write-once static dispatch table for stream handlers.
    /// Populated at startup by the generated StreamRegistry.
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StreamDispatch<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private static Func<IServiceProvider, IStreamRequestHandler<TRequest, TResponse>>? _handler;
        private static Func<TRequest, IServiceProvider, CancellationToken, IAsyncEnumerable<TResponse>>? _pipeline;
        private static bool _isStreamChainCacheable;

        public static Func<IServiceProvider, IStreamRequestHandler<TRequest, TResponse>>? Handler
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _handler;
        }

        /// <summary>
        /// Precompiled stream pipeline delegate. <see langword="null"/> until initialized.
        /// </summary>
        public static Func<TRequest, IServiceProvider, CancellationToken, IAsyncEnumerable<TResponse>>? Pipeline
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pipeline;
        }

        /// <summary>
        /// <see langword="true"/> when the <see cref="StreamPipelineChainHandler{TRequest, TResponse}"/>
        /// is registered as Scoped or Singleton (safe to cache per thread).
        /// </summary>
        public static bool IsStreamChainCacheable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _isStreamChainCacheable);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryInitializeHandler(
            Func<IServiceProvider, IStreamRequestHandler<TRequest, TResponse>> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            return Interlocked.CompareExchange(ref _handler, handler, null) == null;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryInitializePipeline(
            Func<TRequest, IServiceProvider, CancellationToken, IAsyncEnumerable<TResponse>> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            return Interlocked.CompareExchange(ref _pipeline, pipeline, null) == null;
        }

        /// <summary>
        /// Marks the stream pipeline chain as cacheable (Scoped or Singleton lifetime).
        /// Called once at startup by generated code.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkStreamChainCacheable() => Volatile.Write(ref _isStreamChainCacheable, true);
    }
}
