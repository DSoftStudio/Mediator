// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Pipeline behavior that wraps the execution of a stream request handler, the streaming
    /// counterpart of <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// <para>
    /// Behaviors execute in registration order, forming a chain
    /// <c>Behavior1 → Behavior2 → … → Handler</c>.
    /// </para>
    /// <para>
    /// Register them BEFORE <c>PrecompileStreams()</c>. That call decides, per request/response
    /// pair, whether a chain is built at all: if the pair has no behavior registered by then, the
    /// handler streams unwrapped and behaviors added afterwards never run — silently, with no
    /// error. The scan also fixes the chain's lifetime, so a behavior added after it can be
    /// constructed once and shared even when registered <c>Transient</c>.
    /// </para>
    /// <para>
    /// Unlike the request pipeline, the stream pipeline composes behaviors only: there are no
    /// stream pre-processors, post-processors or exception handlers.
    /// </para>
    /// </summary>
    /// <typeparam name="TRequest">The request type, which must implement <see cref="IStreamRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of each item produced by the stream.</typeparam>
    public interface IStreamPipelineBehavior<TRequest, TResponse>
     where TRequest : IStreamRequest<TResponse>
    {
        /// <summary>
        /// Runs around the next step of the stream pipeline for <paramref name="request"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Enumerate <c>next.Handle(request, cancellationToken)</c> and yield what it produces to
        /// continue the chain. Because this is a stream, a behavior can also observe each item as it
        /// passes, drop items, or stop enumerating early.
        /// </para>
        /// <para>
        /// Written as an iterator, the body runs per enumeration rather than per call: nothing
        /// happens until the caller starts consuming the stream. The chain itself is not deferred —
        /// it is built when <c>CreateStream</c> is called.
        /// </para>
        /// <para>
        /// An iterator behavior returns its own stream, so annotate its token parameter with
        /// <c>[EnumeratorCancellation]</c> for the same reason the handler does, and forward the
        /// token when enumerating <paramref name="next"/>.
        /// </para>
        /// </remarks>
        /// <param name="request">The request travelling through the pipeline.</param>
        /// <param name="next">The next step: another behavior, or the terminal handler.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>The stream of responses this behavior exposes to the step before it.</returns>
        IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            IStreamRequestHandler<TRequest, TResponse> next,
            CancellationToken cancellationToken);
    }
}
