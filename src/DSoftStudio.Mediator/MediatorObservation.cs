// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Resolves observation ports per container, once per (thread, provider).
    /// <para>
    /// The negative answer is cached too, and that is the point: without it, a process where any
    /// container registers an observer would make every OTHER container ask its own provider on
    /// every publish, forever, to be told null again — the same shape as the absent-chain defect the
    /// dispatch caches carry an <c>Absent</c> flag for.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MediatorObservation
    {
        [ThreadStatic]
        private static DispatchCacheSlot<IMediatorNotificationObserver>? _notificationSlot;

        /// <summary>
        /// The notification observer this container registered, or <see langword="null"/>. Only ever
        /// reached when <see cref="NotificationPublisherFlag.HasObserver"/> already said some
        /// container in the process has one.
        /// </summary>
        public static IMediatorNotificationObserver? ResolveNotificationObserver(IServiceProvider serviceProvider)
        {
            var slot = _notificationSlot;
            if (serviceProvider is not null && slot is not null && ReferenceEquals(slot.Provider, serviceProvider))
            {
                var cached = slot.Value;
                if (cached is not null)
                    return cached;

                // Provider matches and the value is null: this container was asked already and has
                // none. Asking again can only produce null.
                if (slot.Absent)
                    return null;
            }

            return ResolveSlow(serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IMediatorNotificationObserver? ResolveSlow(IServiceProvider? serviceProvider)
        {
            // Resolve tests for null deliberately -- a RELEASED slot has a null Provider and
            // ReferenceEquals(null, null) is true, so a null provider would otherwise read a
            // "cached" null and fail far from here -- which means a null argument arrives HERE.
            // Naming it blames the parameter the caller passed, instead of surfacing as "provider"
            // from inside GetRequiredService. One test, and only on the miss path.
            ArgumentNullException.ThrowIfNull(serviceProvider);

            // GetServices, not GetService: two adapters observing at once is the normal case -- a
            // tracing bridge and a profiler -- and resolving one silently dropped whichever registered
            // second. One is used directly; several are fanned out through a composite built once.
            var registered = serviceProvider.GetServices<IMediatorNotificationObserver>() as IMediatorNotificationObserver[]
                             ?? [.. serviceProvider.GetServices<IMediatorNotificationObserver>()];

            IMediatorNotificationObserver? observer = registered.Length switch
            {
                0 => null,
                1 => registered[0],
                _ => new CompositeNotificationObserver(registered),
            };

            var slot = _notificationSlot ??= new DispatchCacheSlot<IMediatorNotificationObserver>();

            // Contents before Provider, so a reader that interleaves never matches a stale slot.
            slot.Value = observer;
            slot.Absent = observer is null;
            slot.Provider = serviceProvider;

            DispatchCacheReleaser.Track(serviceProvider, slot);

            return observer;
        }
    }
}
