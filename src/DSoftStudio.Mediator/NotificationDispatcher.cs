// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;


namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Dispatches notifications to all registered <see cref="INotificationHandler{TNotification}"/> instances.
    /// Uses compile-time generated dispatch tables — no runtime service enumeration or reflection.
    /// Executes handlers sequentially in registration order by default.
    /// </summary>
    internal static class NotificationDispatcher
    {
        /// <summary>
        /// Resolves all handlers for <typeparamref name="TNotification"/> from the generated
        /// dispatch table and invokes them sequentially.
        /// Uses <see cref="ValueTask"/>-aware short-circuit: synchronously completed handlers
        /// skip the async state machine entirely.
        /// </summary>
        public static Task DispatchSequential<TNotification>(
            TNotification notification,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            var factories = NotificationDispatch<TNotification>.Handlers;

            if (factories == null || factories.Length == 0)
                return Task.CompletedTask;

            return DispatchCore(notification, serviceProvider, cancellationToken, factories);
        }

        private static async Task DispatchCore<TNotification>(
            TNotification notification,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken,
            Func<IServiceProvider, INotificationHandler<TNotification>>[] factories)
            where TNotification : INotification
        {
            foreach (var factory in factories)
            {
                var task = factory(serviceProvider)
                    .Handle(notification, cancellationToken);

                // Short-circuit: if the handler completed synchronously,
                // skip the await overhead entirely.
                if (!task.IsCompletedSuccessfully)
                    await task.ConfigureAwait(false);
            }
        }
    }

}

