// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles a notification of type <typeparamref name="TNotification"/>.
    /// Multiple handlers can be registered for the same notification type;
    /// all will be invoked, one after another, when the notification is published.
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
        /// By default the handlers for a notification run sequentially — each one completes before
        /// the next starts — and if one throws the rest are skipped.
        /// </para>
        /// <para>
        /// Do NOT rely on the order between handlers. The generated dispatch table and the generated
        /// container registrations are both ordered by handler TYPE NAME, not by the order the
        /// registrations appear in. The sequence is therefore deterministic, but it is not the one the
        /// registration code suggests, and renaming a handler changes it. Handlers that must run in a
        /// given order belong in one handler, or behind a request.
        /// </para>
        /// <para>
        /// Register the built-in <c>ParallelNotificationPublisher</c> to run them concurrently
        /// instead. It queues each handler to the thread pool, so even handlers written in the
        /// completed-task style below run in parallel — which means they must then be safe to run
        /// alongside each other.
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
