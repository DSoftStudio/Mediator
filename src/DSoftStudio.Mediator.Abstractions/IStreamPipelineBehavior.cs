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
    /// <c>Behavior1 → Behavior2 → … → Handler</c>, and must be registered BEFORE
    /// <c>PrecompileStreams()</c>, which is what scans the service collection for them.
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
        /// The body runs per enumeration, not per call: nothing happens until the caller starts
        /// consuming the stream.
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
