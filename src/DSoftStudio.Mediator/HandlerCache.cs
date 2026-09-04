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
    /// Uses <c>[ThreadStatic]</c> to store the last-resolved handler per thread. When the caller's
    /// <see cref="IServiceProvider"/> matches the cached provider (same DI scope), the cached
    /// handler is returned directly — eliminating the ~10 ns <c>GetRequiredService</c> lookup on
    /// every subsequent <c>Send</c>. On scope change the <c>ReferenceEquals</c> guard misses and
    /// re-resolves; after an <c>async/await</c> thread hop it simply misses on the new thread.
    /// </para>
    /// <para>
    /// <b>The cache honours the registered lifetime.</b> Keying on the provider is exactly right
    /// for Singleton and for Scoped — a scope IS a provider. It is wrong for Transient, where the
    /// container promises a fresh instance per resolve. This cache used to store whatever it
    /// resolved, unconditionally, which turned every Transient handler into a per-(thread, provider)
    /// singleton: three <c>Send</c> calls in one scope shared one handler, and with it whatever
    /// per-operation dependency that handler had captured. That is the captive-dependency error the
    /// rest of the library takes pains to avoid — <see cref="HandlerLifetimeOptimizer"/> declines to
    /// raise a handler whose dependency is Transient for precisely this reason, and this cache then
    /// undid the decision one layer down.
    /// </para>
    /// <para>
    /// Cacheability is asked of the CONTAINER, once per (thread, provider), via
    /// <see cref="DispatchCacheability"/> — not of a process-global static, which cannot be right
    /// for two containers that registered the same pair differently.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HandlerCache<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly Type ServiceType = typeof(IRequestHandler<TRequest, TResponse>);


        // Tri-state, read together with _cachedProvider:
        //   provider matches + handler non-null -> cacheable, this is the instance
        //   provider matches + handler null     -> this provider registered the pair Transient
        //   provider differs                    -> cold: ask the container and remember the verdict
        [ThreadStatic]
        private static DispatchCacheSlot<IRequestHandler<TRequest, TResponse>>? _slot;

        /// <summary>
        /// Returns the handler for the given service provider, from the thread-local cache when the
        /// provider matches and its registration allows reuse. Cost: ~1 ns on a hit, ~10 ns
        /// otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IRequestHandler<TRequest, TResponse> Resolve(IServiceProvider serviceProvider)
        {
            // The provider must be non-null: a RELEASED slot has a null Provider, and
            // ReferenceEquals(null, null)
            // is true, so a null provider would hit a "cached" null handler and NRE later, far from here.
            var slot = _slot;
            if (serviceProvider is not null && slot is not null && ReferenceEquals(slot.Provider, serviceProvider))
            {
                var cached = slot.Value;
                if (cached is not null)
                    return cached;

                // Known-Transient for this provider: resolve fresh, and do not re-ask the container.
                return serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            }

            return ResolveSlow(serviceProvider);
        }

        /// <summary>
        /// <see langword="true"/> when the instance this cache just handed out for
        /// <paramref name="serviceProvider"/> may itself be cached by a caller.
        /// <para>
        /// The generated concrete-typed SAFE tier keeps its own <c>[ThreadStatic]</c> pair and resolves
        /// through this cache on its miss path. It must not re-derive the lifetime verdict — that would
        /// put the decision in two places and let them drift. Calling this immediately after
        /// <see cref="Resolve"/> reads the verdict already stored for this (thread, provider).
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCacheableFor(IServiceProvider serviceProvider)
        {
            var slot = _slot;
            return slot is not null
                   && ReferenceEquals(slot.Provider, serviceProvider)
                   && slot.Value is not null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IRequestHandler<TRequest, TResponse> ResolveSlow(IServiceProvider? serviceProvider)
        {
            // Resolve tests for null deliberately -- a RELEASED slot has a null Provider and
            // ReferenceEquals(null, null) is true, so a null provider would otherwise read a
            // "cached" null and fail far from here -- which means a null argument arrives HERE.
            // Naming it blames the parameter the caller passed, instead of surfacing as "provider"
            // from inside GetRequiredService. One test, and only on the miss path.
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            Store(serviceProvider,
                DispatchCacheability.AllowsCaching(serviceProvider, ServiceType) ? handler : null);

            return handler;
        }

        /// <summary>
        /// Fills this thread's slot and registers it with the provider, so disposing that scope can
        /// empty it from whatever thread does the disposing. A null <paramref name="value"/> records
        /// "this container registered the service Transient" — resolve fresh, but do not ask again.
        /// </summary>
        private static void Store(IServiceProvider serviceProvider, IRequestHandler<TRequest, TResponse>? value)
        {
            var slot = _slot ??= new DispatchCacheSlot<IRequestHandler<TRequest, TResponse>>();

            // Value before Provider: a reader that interleaves sees a slot whose provider does not
            // match yet, never one that matches with a stale value.
            slot.Value = value;
            slot.Provider = serviceProvider;

            DispatchCacheReleaser.Track(serviceProvider, slot);
        }
    }
}
