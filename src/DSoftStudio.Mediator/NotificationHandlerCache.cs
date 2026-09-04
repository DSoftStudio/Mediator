// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-thread cache for resolved <see cref="INotificationHandler{TNotification}"/> arrays.
    /// <para>
    /// On cache hit (~1 ns): returns the previously resolved handler array.
    /// On cache miss: resolves all handlers via the factory delegates, caches the result.
    /// The <see cref="IServiceProvider"/> reference equality guard detects scope changes.
    /// </para>
    /// <para>
    /// <b>The cache honours the registered lifetimes.</b> A notification fans out to MANY handlers,
    /// so the array may be reused only when EVERY handler in it may be — one Transient handler
    /// poisons the whole array, because reusing the array reuses that instance too. Each handler's
    /// concrete type is what the generated factories resolve
    /// (<c>GetRequiredService&lt;TheHandler&gt;(sp)</c>), and that concrete type is registered with
    /// the matching lifetime, so that is the type the container is asked about.
    /// </para>
    /// <para>
    /// Before this, the array was cached unconditionally: three <c>Publish</c> calls on one thread
    /// in one scope shared one set of handler instances, whatever their registered lifetime. See
    /// <see cref="HandlerCache{TRequest, TResponse}"/> for the full account.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationHandlerCache<TNotification>
        where TNotification : INotification
    {

        // Tri-state, read together with _cachedProvider:
        //   provider matches + array non-null -> every handler is reusable, this is the array
        //   provider matches + array null     -> at least one handler is Transient here
        //   provider differs                  -> cold: ask the container and remember the verdict
        [ThreadStatic]
        private static DispatchCacheSlot<INotificationHandler<TNotification>[]>? _slot;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static INotificationHandler<TNotification>[] Resolve(
            IServiceProvider serviceProvider,
            Func<IServiceProvider, INotificationHandler<TNotification>>[] factories)
        {
            var slot = _slot;
            if (serviceProvider is not null && slot is not null && ReferenceEquals(slot.Provider, serviceProvider))
            {
                var cached = slot.Value;
                if (cached is not null)
                    return cached;

                // At least one handler is Transient for this provider: resolve fresh every time, and
                // do not re-ask the container.
                return Create(serviceProvider, factories);
            }

            return ResolveSlow(serviceProvider, factories);
        }

        /// <summary>
        /// <see langword="true"/> when the array this cache just handed out for
        /// <paramref name="serviceProvider"/> may itself be cached by a caller — that is, when every
        /// handler in it is reusable. See <see cref="HandlerCache{TRequest, TResponse}.IsCacheableFor"/>
        /// for why the generated fast path asks rather than re-deriving.
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
        private static INotificationHandler<TNotification>[] ResolveSlow(
            IServiceProvider? serviceProvider,
            Func<IServiceProvider, INotificationHandler<TNotification>>[] factories)
        {
            // Resolve tests for null deliberately -- a RELEASED slot has a null Provider and
            // ReferenceEquals(null, null) is true, so a null provider would otherwise read a
            // "cached" null and fail far from here -- which means a null argument arrives HERE.
            // Naming it blames the parameter the caller passed, instead of surfacing as "provider"
            // from inside GetRequiredService. One test, and only on the miss path.
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var handlers = Create(serviceProvider, factories);

            Store(serviceProvider, AllReusable(serviceProvider, handlers) ? handlers : null);

            return handlers;
        }

        private static INotificationHandler<TNotification>[] Create(
            IServiceProvider serviceProvider,
            Func<IServiceProvider, INotificationHandler<TNotification>>[] factories)
        {
            var handlers = new INotificationHandler<TNotification>[factories.Length];
            for (int i = 0; i < factories.Length; i++)
                handlers[i] = factories[i](serviceProvider);

            return handlers;
        }

        private static bool AllReusable(
            IServiceProvider serviceProvider,
            INotificationHandler<TNotification>[] handlers)
        {
            for (int i = 0; i < handlers.Length; i++)
            {
                if (!DispatchCacheability.AllowsCaching(serviceProvider, handlers[i].GetType()))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Fills this thread's slot and registers it with the provider, so disposing that scope can
        /// empty it from whatever thread does the disposing. A null <paramref name="value"/> records
        /// "this container registered the service Transient" — resolve fresh, but do not ask again.
        /// </summary>
        private static void Store(IServiceProvider serviceProvider, INotificationHandler<TNotification>[]? value)
        {
            var slot = _slot ??= new DispatchCacheSlot<INotificationHandler<TNotification>[]>();

            // Value before Provider: a reader that interleaves sees a slot whose provider does not
            // match yet, never one that matches with a stale value.
            slot.Value = value;
            slot.Provider = serviceProvider;

            DispatchCacheReleaser.Track(serviceProvider, slot);
        }
    }
}
