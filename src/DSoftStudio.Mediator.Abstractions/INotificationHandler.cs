// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles a notification of type <typeparamref name="TNotification"/>.
    /// Multiple handlers can be registered for the same notification type;
    /// all will be invoked sequentially when the notification is published.
    /// </summary>
    /// <typeparam name="TNotification">The notification type, which must implement <see cref="INotification"/>.</typeparam>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles <paramref name="notification"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default handlers run sequentially, in registration order, and if one throws the rest
        /// are skipped. Register the built-in <c>ParallelNotificationPublisher</c> to run them in
        /// parallel instead.
        /// </para>
        /// <para>
        /// When the work is synchronous, return a completed task rather than marking the method
        /// <c>async</c>. An <c>async</c> method that never suspends still builds an async state
        /// machine, and a notification is dispatched to every registered handler, so that cost is
        /// multiplied:
        /// </para>
        /// <code>
        /// public Task Handle(UserCreated notification, CancellationToken ct)
        /// {
        ///     Console.WriteLine($"Sending welcome email to {notification.Id}");
        ///     return Task.CompletedTask;
        /// }
        /// </code>
        /// <para>
        /// Use <c>async</c> when the handler genuinely awaits something.
        /// </para>
        /// </remarks>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when this handler is done.</returns>
        Task Handle(TNotification notification, CancellationToken cancellationToken);
    }
}
