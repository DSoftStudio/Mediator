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
        // One word, not two flags in two statics. The dispatch bodies ask "is this container still
        // plain?" on their cold path, and that has to stay ONE static read: a second static class
        // would be a second static base to touch. Bit 1 = a custom publisher is registered,
        // bit 2 = a notification observer is registered. Both are one-way.
        private const int CustomPublisherBit = 1;
        private const int ObserverBit = 2;

        private static int _flags;

        /// <summary>
        /// <see langword="true"/> when anything at all has taken this process off the plain
        /// notification path — a custom publisher, an observer, or both. This is the read the
        /// dispatch paths make; the two specific properties below are for cold-path decisions.
        /// </summary>
        public static bool NotPlain
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _flags) != 0;
        }

        /// <summary>
        /// <see langword="true"/> when a custom <see cref="Abstractions.INotificationPublisher"/>
        /// is registered. Default is <see langword="false"/>.
        /// </summary>
        public static bool HasCustomPublisher
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Volatile.Read(ref _flags) & CustomPublisherBit) != 0;
        }

        /// <summary>
        /// <see langword="true"/> when an <see cref="Abstractions.IMediatorNotificationObserver"/>
        /// is registered. Like a custom publisher, this is knowable before the container is built,
        /// which is what lets the armed tier stand down at startup instead of every publish paying
        /// for the possibility.
        /// </summary>
        public static bool HasObserver
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Volatile.Read(ref _flags) & ObserverBit) != 0;
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

            if (serviceProvider.GetService(typeof(Abstractions.IMediatorNotificationObserver)) is not null)
                ObserverAppeared();
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
        private static void PublisherAppeared() => Appeared(CustomPublisherBit, "custom-publisher");

        /// <summary>
        /// Marks a notification observer as registered. Same one-way, disarm-first shape as a custom
        /// publisher, and for the same reason: an ARMED holder dispatches straight to the handlers,
        /// so it would run right past an observer that appeared after it armed.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ObserverAppeared() => Appeared(ObserverBit, "notification-observer");

        private static void Appeared(int bit, string reason)
        {
            // Disarm BEFORE the flag write: a dispatch landing between the two reads a null holder
            // and falls to the slow path, which is the same dispatch every publish got before the
            // registration. The EventSource write stays outside the latch lock -- in-proc listener
            // callbacks must never run under it.
            var disarmed = AggressiveDispatchLatch.DisarmNotifications();
            Interlocked.Or(ref _flags, bit);
            if (disarmed > 0)
                AggressiveDispatchEventSource.Log.AggressivePoisoned(reason, disarmed);
        }

        /// <summary>
        /// Test-only: clears the write-once flag. The flag is process-global; test suites that
        /// exercise custom publishers need isolation between cases (ADR-0066 eligibility reads it).
        /// </summary>
        internal static void ResetForTests() => Volatile.Write(ref _flags, 0);
    }
}
