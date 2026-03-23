// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// AOT-safe static dispatch table for <see cref="IPublisher.Publish(object, CancellationToken)"/>.
    /// <para>
    /// Populated at startup by the generated <c>NotificationRegistry.Register()</c>.
    /// Each notification type gets a compile-time generated delegate — no
    /// <c>MakeGenericType</c>, no <c>Expression.Compile</c>, no reflection.
    /// After all registrations complete, <see cref="Freeze"/> converts the table to
    /// a <see cref="FrozenDictionary{TKey, TValue}"/> for optimal concurrent read performance.
    /// </para>
    /// <para>
    /// When a source generator is present, <see cref="SetGeneratedSwitch"/> replaces
    /// the default FrozenDictionary lookup with a compile-time type switch, eliminating
    /// the dictionary probe + delegate invocation (~1.5 ns saving).
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationObjectDispatch
    {
        /// <summary>
        /// Dispatch delegate signature for runtime-typed notification publishing.
        /// </summary>
        public delegate Task DispatchDelegate(
            object notification,
            IServiceProvider serviceProvider,
            INotificationPublisher? publisher,
            CancellationToken cancellationToken);

        // Mutable during registration; frozen snapshot created by Freeze().
        // ConcurrentDictionary ensures thread-safety when multiple hosts or test
        // runners call Register() in parallel before Freeze() is invoked.
        private static readonly ConcurrentDictionary<Type, DispatchDelegate> _mutableDispatchers = new();
        private static FrozenDictionary<Type, DispatchDelegate> _dispatchers = FrozenDictionary<Type, DispatchDelegate>.Empty;

        // Fast-path dispatch — defaults to FrozenDictionary; overridden by generated type switch.
        private static DispatchDelegate _dispatch = DispatchFallback;

        /// <summary>
        /// Registers a compile-time generated dispatch delegate for <typeparamref name="TNotification"/>.
        /// Called once at startup by the generated <c>NotificationRegistry</c>.
        /// Idempotent — safe to call multiple times (e.g. in test isolation).
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register<TNotification>(DispatchDelegate dispatcher)
            where TNotification : INotification
        {
            _mutableDispatchers[typeof(TNotification)] = dispatcher;
        }

        /// <summary>
        /// Creates a <see cref="FrozenDictionary{TKey, TValue}"/> snapshot from all
        /// registered dispatchers for optimal read performance.
        /// Called at the end of <c>PrecompileNotifications()</c>.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Freeze()
        {
            _dispatchers = _mutableDispatchers.ToFrozenDictionary();
        }

        /// <summary>
        /// Replaces the default FrozenDictionary-based dispatch with a source-generated
        /// type switch for optimal performance. Called once by the generated NotificationRegistry.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetGeneratedSwitch(DispatchDelegate generatedSwitch)
        {
            _dispatch = generatedSwitch;
        }

        /// <summary>
        /// Dispatches a notification using the active dispatch strategy.
        /// When a source generator is present, uses the compile-time type switch;
        /// otherwise falls back to FrozenDictionary lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task Dispatch(
            object notification,
            IServiceProvider serviceProvider,
            INotificationPublisher? publisher,
            CancellationToken cancellationToken)
        {
            return _dispatch(notification, serviceProvider, publisher, cancellationToken);
        }

        /// <summary>
        /// FrozenDictionary-based dispatch — used as the default before
        /// <see cref="SetGeneratedSwitch"/> is called, and as the fallback
        /// for notification types not known at compile time.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Task DispatchFallback(
            object notification,
            IServiceProvider serviceProvider,
            INotificationPublisher? publisher,
            CancellationToken cancellationToken)
        {
            if (_dispatchers.TryGetValue(notification.GetType(), out var dispatcher))
                return dispatcher(notification, serviceProvider, publisher, cancellationToken);

            ThrowNoHandler(notification);
            return Task.CompletedTask; // unreachable
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNoHandler(object notification)
        {
            if (notification is not INotification)
            {
                throw new ArgumentException(
                    $"Object of type {notification.GetType().Name} does not implement {nameof(INotification)}.",
                    nameof(notification));
            }

            throw new InvalidOperationException(
                $"No notification handler registered for {notification.GetType().Name}. " +
                "Ensure PrecompileNotifications() is called during service configuration.");
        }
    }
}
