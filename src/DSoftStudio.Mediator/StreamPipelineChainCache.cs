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
        private static DispatchCacheSlot<StreamPipelineChainHandler<TRequest, TResponse>>? _slot;

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
            // The provider must be non-null: a RELEASED slot has a null Provider, and
            // ReferenceEquals(null, null)
            // is true, so a null provider would hit a "cached" null chain and fail somewhere else.
            var slot = _slot;
            if (serviceProvider is not null && slot is not null && ReferenceEquals(slot.Provider, serviceProvider))
            {
                var cached = slot.Value;
                if (cached is not null)
                    return cached;

                // This container has no chain for the pair. Nothing gates this call the way
                // RequestDispatch.HasPipelineChain gates the request side, so before this branch
                // existed EVERY CreateStream of a pair with no stream behaviors — the common case —
                // paid a GetService that could only ever return null.
                if (slot.Absent)
                    return null;

                return serviceProvider.GetService<StreamPipelineChainHandler<TRequest, TResponse>>();
            }

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static StreamPipelineChainHandler<TRequest, TResponse>? ResolveSlow(IServiceProvider? serviceProvider)
        {
            // Resolve tests for null deliberately -- a RELEASED slot has a null Provider and
            // ReferenceEquals(null, null) is true, so a null provider would otherwise read a
            // "cached" null and fail far from here -- which means a null argument arrives HERE.
            // Naming it blames the parameter the caller passed, instead of surfacing as "provider"
            // from inside GetRequiredService. One test, and only on the miss path.
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var chain = serviceProvider.GetService<StreamPipelineChainHandler<TRequest, TResponse>>();

            Store(serviceProvider,
                chain is not null && DispatchCacheability.AllowsCaching(serviceProvider, ServiceType) ? chain : null,
                absent: chain is null);

            return chain;
        }

        /// <summary>
        /// Fills this thread's slot and registers it with the provider, so disposing that scope can
        /// empty it from whatever thread does the disposing. A null <paramref name="value"/> with
        /// <paramref name="absent"/> false records "this container registered the chain Transient" —
        /// resolve fresh each time; with it true, "this container has no chain" — stop asking.
        /// </summary>
        private static void Store(
            IServiceProvider serviceProvider,
            StreamPipelineChainHandler<TRequest, TResponse>? value,
            bool absent)
        {
            var slot = _slot ??= new DispatchCacheSlot<StreamPipelineChainHandler<TRequest, TResponse>>();

            // Contents before Provider: a reader that interleaves sees a slot whose provider does not
            // match yet, never one that matches with stale contents.
            slot.Value = value;
            slot.Absent = absent;
            slot.Provider = serviceProvider;

            DispatchCacheReleaser.Track(serviceProvider, slot);
        }
    }
}
