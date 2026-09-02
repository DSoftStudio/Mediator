// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles exceptions thrown during request pipeline execution.
    /// <para>
    /// Registered as an open generic or for specific request types.
    /// If the handler sets <see cref="RequestExceptionHandlerState{TResponse}.Handled"/>
    /// to <c>true</c> and provides a response, the exception is suppressed and the
    /// response is returned to the caller. Otherwise, the exception propagates.
    /// </para>
    /// </summary>
    public interface IRequestExceptionHandler<in TRequest, TResponse>
    {
        /// <summary>
        /// Observes <paramref name="exception"/>, and decides whether to suppress it.
        /// </summary>
        /// <remarks>
        /// Set <see cref="RequestExceptionHandlerState{TResponse}.Handled"/> through
        /// <paramref name="state"/>, supplying a response, to suppress the exception and return that
        /// response to the caller. Leave it alone and the exception propagates.
        /// </remarks>
        /// <param name="request">The request whose dispatch threw.</param>
        /// <param name="exception">The exception thrown during dispatch.</param>
        /// <param name="state">Carries the decision, and the response to return when suppressing.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when this exception handler is done.</returns>
        ValueTask Handle(
            TRequest request,
            Exception exception,
            RequestExceptionHandlerState<TResponse> state,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Mutable state passed to <see cref="IRequestExceptionHandler{TRequest, TResponse}"/>.
    /// Call <see cref="SetHandled"/> to suppress the exception and provide a fallback response.
    /// </summary>
    public sealed class RequestExceptionHandlerState<TResponse>
    {
        /// <summary>Whether the exception has been handled and should not propagate.</summary>
        public bool Handled { get; private set; }

        /// <summary>The fallback response to return if the exception is handled.</summary>
        public TResponse? Response { get; private set; }

        /// <summary>
        /// Marks the exception as handled and provides a fallback response.
        /// </summary>
        public void SetHandled(TResponse response)
        {
            Handled = true;
            Response = response;
        }
    }
}
