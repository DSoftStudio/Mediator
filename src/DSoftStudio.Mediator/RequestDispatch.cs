// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Static dispatch metadata for a specific <c>&lt;TRequest, TResponse&gt;</c> pair.
    /// The CLR creates one specialization per closed generic type, giving O(1) lookup
    /// without any dictionary or concurrent collection.
    /// <para>
    /// The flags are set once at startup by source-generated code and read on the hot path
    /// (by the Send interceptor and <c>Mediator.Send</c>) to choose between the pipeline-chain
    /// and direct-handler dispatch paths with a single static-field read.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class RequestDispatch<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static bool _hasPipelineChain;
        private static bool _isPipelineChainCacheable;

        /// <summary>
        /// <see langword="true"/> when a <see cref="PipelineChainHandler{TRequest, TResponse}"/>
        /// is registered in DI (behaviors / processors / exception handlers exist).
        /// Used by the interceptor for zero-delegate dispatch: static field read + branch.
        /// </summary>
        public static bool HasPipelineChain
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _hasPipelineChain);
        }

        /// <summary>
        /// <see langword="true"/> when the <see cref="PipelineChainHandler{TRequest, TResponse}"/>
        /// is registered as Scoped or Singleton (safe to cache per thread).
        /// <see langword="false"/> for Transient registrations (must resolve fresh each call).
        /// </summary>
        public static bool IsPipelineChainCacheable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _isPipelineChainCacheable);
        }

        /// <summary>
        /// Marks that a <see cref="PipelineChainHandler{TRequest, TResponse}"/> is registered
        /// in DI for this request type. Called once at startup by generated code.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkPipelineChainRegistered() => Volatile.Write(ref _hasPipelineChain, true);

        /// <summary>
        /// Marks the pipeline chain as cacheable (Scoped or Singleton lifetime).
        /// Called once at startup by generated code alongside <see cref="MarkPipelineChainRegistered"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkPipelineChainCacheable() => Volatile.Write(ref _isPipelineChainCacheable, true);
    }
}
