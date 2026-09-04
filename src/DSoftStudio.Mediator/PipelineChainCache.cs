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
    /// per (thread, provider) when the chain's registered lifetime allows it.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> to hold the last-resolved chain per thread. When the caller's
    /// <see cref="IServiceProvider"/> matches the cached one — the same DI scope — the cached chain is
    /// returned directly, avoiding the ~10 ns <c>GetService</c> lookup on every subsequent
    /// <c>Send</c>.
    /// </para>
    /// <para>
    /// <b>Cacheability is decided here, not by the caller.</b> A Transient chain must never be cached,
    /// and that used to be a ternary in the emitted dispatch body — duplicated across all FOUR Send
    /// bodies. It is one fact about the pair; it belongs on the miss path, which is cold.
    /// </para>
    /// <para>
    /// <b>And it is asked of the CONTAINER, not of a static.</b>
    /// <see cref="RequestDispatch{TRequest, TResponse}.IsPipelineChainCacheable"/> is one static per
    /// closed generic pair, so it is process-global and monotonic, while a chain's lifetime is decided
    /// per container. Two containers disagree routinely — a test suite, a modular monolith, a host that
    /// rebuilds its provider — and the first one to register a Scoped or Singleton chain latched that
    /// flag on for every container that followed, so a later container's genuinely Transient chain was
    /// cached and pinned. <see cref="DispatchCacheability"/> answers per container instead. The static
    /// flag remains for compatibility but no longer decides caching.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class PipelineChainCache<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly Type ServiceType = typeof(PipelineChainHandler<TRequest, TResponse>);


        // Tri-state, read together with _cachedProvider:
        //   provider matches + chain non-null -> reusable, this is the chain
        //   provider matches + chain null + Absent   -> this container has no chain for the pair:
        //                                                answer null without asking again
        //   provider matches + chain null             -> Transient here: resolve fresh every time
        //   provider differs                  -> cold: ask the container and remember the verdict
        [ThreadStatic]
        private static DispatchCacheSlot<PipelineChainHandler<TRequest, TResponse>>? _slot;

        /// <summary>
        /// Returns the pipeline chain for the given provider, from the thread-local cache when the
        /// provider matches and its registration allows reuse. Cost: ~1 ns on a hit, ~10 ns otherwise.
        /// Returns <see langword="null"/> when this container registers no chain for the pair.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PipelineChainHandler<TRequest, TResponse>? Resolve(IServiceProvider serviceProvider)
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

                // This container has no chain for the pair. Asking again can only return null, and a
                // GetService that always fails is not free: before this branch existed, every dispatch
                // through such a container paid one, permanently.
                if (slot.Absent)
                    return null;

                return serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();
            }

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PipelineChainHandler<TRequest, TResponse>? ResolveSlow(IServiceProvider? serviceProvider)
        {
            // Resolve tests for null deliberately -- a RELEASED slot has a null Provider and
            // ReferenceEquals(null, null) is true, so a null provider would otherwise read a
            // "cached" null and fail far from here -- which means a null argument arrives HERE.
            // Naming it blames the parameter the caller passed, instead of surfacing as "provider"
            // from inside GetRequiredService. One test, and only on the miss path.
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var chain = serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();

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
            PipelineChainHandler<TRequest, TResponse>? value,
            bool absent)
        {
            var slot = _slot ??= new DispatchCacheSlot<PipelineChainHandler<TRequest, TResponse>>();

            // Contents before Provider: a reader that interleaves sees a slot whose provider does not
            // match yet, never one that matches with stale contents.
            slot.Value = value;
            slot.Absent = absent;
            slot.Provider = serviceProvider;

            DispatchCacheReleaser.Track(serviceProvider, slot);
        }
    }
}
