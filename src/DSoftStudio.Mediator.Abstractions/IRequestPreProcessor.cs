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
    /// Multiple pre-processors execute in registration order.
    /// If a pre-processor throws, the handler is not invoked.
    /// </para>
    /// </summary>
    public interface IRequestPreProcessor<in TRequest>
    {
        /// <summary>
        /// Runs before the handler for <paramref name="request"/>.
        /// </summary>
        /// <remarks>
        /// Throwing here stops the dispatch: the handler is not invoked. When the work is
        /// synchronous, return a completed value rather than marking the method <c>async</c>.
        /// </remarks>
        /// <param name="request">The request about to be handled.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when this pre-processor is done.</returns>
        ValueTask Process(TRequest request, CancellationToken cancellationToken);
    }
}
