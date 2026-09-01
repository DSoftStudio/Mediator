// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Global write-once flag indicating whether an <see cref="Abstractions.INotificationPublisher"/>
    /// is registered in the DI container. The Publish interceptor reads this to skip the
    /// per-call <c>GetService&lt;INotificationPublisher&gt;</c> lookup when no custom publisher exists.
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationPublisherFlag
    {
        private static bool _hasCustomPublisher;

        /// <summary>
        /// <see langword="true"/> when a custom <see cref="Abstractions.INotificationPublisher"/>
        /// is registered. Default is <see langword="false"/>.
        /// </summary>
        public static bool HasCustomPublisher
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _hasCustomPublisher);
        }

        /// <summary>
        /// Probes the service provider for a registered <see cref="Abstractions.INotificationPublisher"/>
        /// and sets the flag accordingly. Safe to call multiple times (idempotent).
        /// Called once at startup by generated <c>PrecompileNotifications()</c>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void DetectFrom(IServiceProvider serviceProvider)
        {
            if (serviceProvider.GetService(typeof(Abstractions.INotificationPublisher)) is not null)
                PublisherAppeared();
        }

        /// <summary>
        /// Marks a custom publisher as registered without probing DI.
        /// Used when the registration is known at compile time (e.g. generated code).
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void MarkRegistered() => PublisherAppeared();

        /// <summary>
        /// ADR-0066: a custom publisher changes Publish semantics for every notification type,
        /// so any ARMED notification fast path must stand down. Disarm happens BEFORE the flag
        /// write: post-disarm dispatches read a null holder and fall to the slow path; the brief
        /// window where the flag is still false yields the sequential dispatch every
        /// pre-registration publish already got. The EventSource write runs outside the latch
        /// lock (in-proc listener callbacks must never run under it).
        /// </summary>
        private static void PublisherAppeared()
        {
            var disarmed = AggressiveDispatchLatch.DisarmNotifications();
            Volatile.Write(ref _hasCustomPublisher, true);
            if (disarmed > 0)
                AggressiveDispatchEventSource.Log.AggressivePoisoned("custom-publisher", disarmed);
        }

        /// <summary>
        /// Test-only: clears the write-once flag. The flag is process-global; test suites that
        /// exercise custom publishers need isolation between cases (ADR-0066 eligibility reads it).
        /// </summary>
        internal static void ResetForTests() => Volatile.Write(ref _hasCustomPublisher, false);
    }
}
