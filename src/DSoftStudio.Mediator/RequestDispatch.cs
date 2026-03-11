// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Write-once static dispatch table for a specific <c>&lt;TRequest, TResponse&gt;</c> pair.
    /// The CLR creates one specialization per closed generic type, giving O(1) lookup
    /// without any dictionary or concurrent collection.
    /// <para>
    /// Populated once at startup by source-generated code. After initialization,
    /// the pipeline cannot be overwritten — <see cref="TryInitialize"/> uses
    /// <see cref="Interlocked.CompareExchange{T}"/> to enforce write-once semantics.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class RequestDispatch<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static Func<TRequest, IServiceProvider, CancellationToken, ValueTask<TResponse>>? _pipeline;
        private static bool _hasPipelineChain;

        /// <summary>
        /// The cached pipeline dispatch delegate. <see langword="null"/> until initialized.
        /// Hot-path read — inlined by the JIT to a single static field load.
        /// </summary>
        public static Func<TRequest, IServiceProvider, CancellationToken, ValueTask<TResponse>>? Pipeline
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pipeline;
        }

        /// <summary>
        /// <see langword="true"/> when a <see cref="PipelineChainHandler{TRequest, TResponse}"/>
        /// is registered in DI (behaviors / processors / exception handlers exist).
        /// Used by the interceptor for zero-delegate dispatch: static field read + branch.
        /// </summary>
        public static bool HasPipelineChain
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _hasPipelineChain;
        }

        /// <summary>
        /// Atomically sets the pipeline if not yet initialized. Returns <see langword="true"/>
        /// if this call performed the initialization; <see langword="false"/> if already set.
        /// Thread-safe, lock-free, zero-cost on the read path.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryInitialize(
            Func<TRequest, IServiceProvider, CancellationToken, ValueTask<TResponse>> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            return Interlocked.CompareExchange(ref _pipeline, pipeline, null) == null;
        }

        /// <summary>
        /// Marks that a <see cref="PipelineChainHandler{TRequest, TResponse}"/> is registered
        /// in DI for this request type. Called once at startup by generated code.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkPipelineChainRegistered() => _hasPipelineChain = true;
    }
}
