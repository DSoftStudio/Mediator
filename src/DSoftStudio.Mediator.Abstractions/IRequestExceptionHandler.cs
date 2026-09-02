// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles exceptions thrown by the pre-processors, the behavior chain or the request handler.
    /// <para>
    /// Registered as an open generic or for specific request types.
    /// If the handler calls <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/>,
    /// the exception is suppressed and that response is returned to the caller.
    /// Otherwise, the exception propagates.
    /// </para>
    /// <para>
    /// The guarded region covers the <see cref="IRequestPreProcessor{TRequest}"/> stage, the behavior
    /// chain and the terminal handler. It deliberately stops short of the
    /// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> stage: by the time a post-processor
    /// runs, a response already exists, so substituting a different one would leave the
    /// post-processors that already ran having observed a response that is not the one returned. A
    /// post-processor that throws therefore propagates to the caller.
    /// </para>
    /// </summary>
    public interface IRequestExceptionHandler<in TRequest, TResponse>
    {
        /// <summary>
        /// Observes <paramref name="exception"/>, and decides whether to suppress it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/> on
        /// <paramref name="state"/> to suppress the exception and return that response to the
        /// caller. Leave it alone and the exception propagates.
        /// </para>
        /// <para>
        /// Handlers are invoked in registration order and the FIRST one to call <c>SetHandled</c>
        /// wins: the remaining handlers are not invoked. If none does, the original exception is
        /// rethrown with its stack intact. Suppressing does not skip the post-processors — they run
        /// on the substituted response, as on a successful dispatch.
        /// </para>
        /// <para>
        /// An exception thrown by this method is not caught: it replaces the original one.
        /// </para>
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
