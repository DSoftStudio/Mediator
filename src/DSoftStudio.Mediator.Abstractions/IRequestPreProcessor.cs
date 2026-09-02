// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Runs before the request handler executes.
    /// <para>
    /// Use for cross-cutting concerns that only need a "before" hook:
    /// validation, authorization, logging, request enrichment.
    /// Simpler than <see cref="IPipelineBehavior{TRequest, TResponse}"/> —
    /// no <c>next</c> parameter, no chain responsibility.
    /// </para>
    /// <para>
    /// Multiple pre-processors execute in registration order. If one throws, neither the behaviors
    /// nor the handler are invoked and the post-processors are skipped.
    /// </para>
    /// <para>
    /// The pre-processor stage runs INSIDE the region guarded by
    /// <see cref="IRequestExceptionHandler{TRequest, TResponse}"/>, so a registered exception handler
    /// does see a throw from here and may substitute a response for it — which is the point of
    /// throwing from a validation or authorization pre-processor.
    /// </para>
    /// </summary>
    public interface IRequestPreProcessor<in TRequest>
    {
        /// <summary>
        /// Runs before the handler for <paramref name="request"/>.
        /// </summary>
        /// <remarks>
        /// Throwing here stops the dispatch: the handler is not invoked. A registered
        /// <see cref="IRequestExceptionHandler{TRequest, TResponse}"/> is consulted and may suppress
        /// the exception, in which case its response is returned and the post-processors run on it.
        /// When the work is synchronous, return a completed value rather than marking the method
        /// <c>async</c>.
        /// </remarks>
        /// <param name="request">The request about to be handled.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when this pre-processor is done.</returns>
        ValueTask Process(TRequest request, CancellationToken cancellationToken);
    }
}
