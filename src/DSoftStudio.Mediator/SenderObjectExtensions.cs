// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Runtime-typed <c>Send(object)</c> extension for <see cref="ISender"/>.
    /// <para>
    /// Designed for message bus / command queue scenarios where the consumer
    /// deserializes a request from the wire and only has an <see cref="object"/> reference.
    /// Uses a compile-time generated dispatch table — no reflection, AOT-safe.
    /// </para>
    /// <para>
    /// This is an extension method (not an interface method) so that the generated typed
    /// extension methods (e.g. <c>Send(this ISender, Ping)</c>) are preferred by overload
    /// resolution when the compile-time type is known.
    /// </para>
    /// </summary>
    public static class SenderObjectExtensions
    {
        /// <summary>
        /// Sends a request whose compile-time type is unknown (runtime dispatch).
        /// The object must be a type discovered at compile time by the source generator.
        /// <para>
        /// Uses the compile-time generated <see cref="RequestObjectDispatch"/> table.
        /// No <c>MakeGenericType</c>, no <c>Expression.Compile</c>, no reflection.
        /// </para>
        /// </summary>
        /// <returns>The handler response boxed as <see cref="object"/>. May be <see langword="null"/> for <see cref="Unit"/> responses.</returns>
        public static ValueTask<object?> Send(this ISender sender, object request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(request);

            var serviceProvider = ((IServiceProviderAccessor)sender).ServiceProvider;
            return RequestObjectDispatch.Dispatch(request, serviceProvider, cancellationToken);
        }
    }
}
