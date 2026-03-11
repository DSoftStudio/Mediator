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
