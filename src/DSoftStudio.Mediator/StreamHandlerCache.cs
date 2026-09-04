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
    /// <para>
    /// <b>The cache honours the registered lifetime</b>, asking the container about the resolved
    /// handler's concrete type once per (thread, provider) through
    /// <see cref="DispatchCacheability"/>. Caching keyed on the provider
    /// is right for Singleton and Scoped and wrong for Transient; this cache used to store whatever
    /// the factory returned, so a Transient stream handler was pinned for every later
    /// <c>CreateStream</c> on that provider. See <see cref="HandlerCache{TRequest, TResponse}"/> for
    /// the full account.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StreamHandlerCache<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {

        // Tri-state: see HandlerCache<,> for the protocol.
        [ThreadStatic]
        private static DispatchCacheSlot<IStreamRequestHandler<TRequest, TResponse>>? _slot;

        /// <summary>
        /// Returns the stream handler for the given service provider, from the thread-local cache
        /// when the provider matches and its registration allows reuse. Cost: ~1 ns on a hit,
        /// ~10 ns otherwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IStreamRequestHandler<TRequest, TResponse> Resolve(IServiceProvider serviceProvider)
        {
            var slot = _slot;
            if (serviceProvider is not null && slot is not null && ReferenceEquals(slot.Provider, serviceProvider))
            {
                var cached = slot.Value;
                if (cached is not null)
                    return cached;

                return Create(serviceProvider);
            }

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IStreamRequestHandler<TRequest, TResponse> ResolveSlow(IServiceProvider? serviceProvider)
        {
            // Resolve tests for null deliberately -- a RELEASED slot has a null Provider and
            // ReferenceEquals(null, null) is true, so a null provider would otherwise read a
            // "cached" null and fail far from here -- which means a null argument arrives HERE.
            // Naming it blames the parameter the caller passed, instead of surfacing as "provider"
            // from inside GetRequiredService. One test, and only on the miss path.
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var handler = Create(serviceProvider);

            // Ask about the CONCRETE type, not IStreamRequestHandler<,>: StreamDispatch's factory is
            // emitted as GetRequiredService<TheHandler>(sp), and the generator registers that
            // concrete type separately with the matching lifetime ("Notification and stream dispatch
            // tables resolve by CONCRETE type" - DependencyInjectionGenerator). Asking about the
            // interface would read a descriptor this path never resolves.
            Store(serviceProvider,
                DispatchCacheability.AllowsCaching(serviceProvider, handler.GetType()) ? handler : null);

            return handler;
        }

        private static IStreamRequestHandler<TRequest, TResponse> Create(IServiceProvider serviceProvider)
        {
            var factory = StreamDispatch<TRequest, TResponse>.Handler
                ?? throw new InvalidOperationException(
                    $"Stream handler for {typeof(TRequest).Name} not registered. " +
                    "Ensure PrecompileStreams() is called during service configuration.");

            return factory(serviceProvider);
        }

        /// <summary>
        /// Fills this thread's slot and registers it with the provider, so disposing that scope can
        /// empty it from whatever thread does the disposing. A null <paramref name="value"/> records
        /// "this container registered the service Transient" — resolve fresh, but do not ask again.
        /// </summary>
        private static void Store(IServiceProvider serviceProvider, IStreamRequestHandler<TRequest, TResponse>? value)
        {
            var slot = _slot ??= new DispatchCacheSlot<IStreamRequestHandler<TRequest, TResponse>>();

            // Value before Provider: a reader that interleaves sees a slot whose provider does not
            // match yet, never one that matches with a stale value.
            slot.Value = value;
            slot.Provider = serviceProvider;

            DispatchCacheReleaser.Track(serviceProvider, slot);
        }
    }
}
