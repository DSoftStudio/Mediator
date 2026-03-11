// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Write-once static dispatch table for notification handlers.
    /// Populated at startup by the generated NotificationRegistry.
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationDispatch<TNotification>
        where TNotification : INotification
    {
        private static Func<IServiceProvider, INotificationHandler<TNotification>>[]? _handlers;

        /// <summary>
        /// The cached handler factory delegates. <see langword="null"/> until initialized.
        /// </summary>
        public static Func<IServiceProvider, INotificationHandler<TNotification>>[]? Handlers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _handlers;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryInitialize(
            Func<IServiceProvider, INotificationHandler<TNotification>>[] handlers)
        {
            ArgumentNullException.ThrowIfNull(handlers);
            return Interlocked.CompareExchange(ref _handlers, handlers, null) == null;
        }
    }
}
