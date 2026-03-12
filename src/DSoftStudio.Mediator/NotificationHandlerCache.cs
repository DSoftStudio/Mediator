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
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotificationHandlerCache<TNotification>
        where TNotification : INotification
    {
        [ThreadStatic]
        private static IServiceProvider? _cachedProvider;

        [ThreadStatic]
        private static INotificationHandler<TNotification>[]? _cachedHandlers;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static INotificationHandler<TNotification>[] Resolve(
            IServiceProvider serviceProvider,
            Func<IServiceProvider, INotificationHandler<TNotification>>[] factories)
        {
            if (ReferenceEquals(_cachedProvider, serviceProvider))
                return _cachedHandlers!;

            var handlers = new INotificationHandler<TNotification>[factories.Length];
            for (int i = 0; i < factories.Length; i++)
                handlers[i] = factories[i](serviceProvider);

            _cachedProvider = serviceProvider;
            _cachedHandlers = handlers;
            return handlers;
        }
    }
}
