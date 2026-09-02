// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Runs after the request handler executes successfully.
    /// <para>
    /// Use for cross-cutting concerns that only need an "after" hook:
    /// audit logging, caching responses, metrics collection.
    /// Simpler than <see cref="IPipelineBehavior{TRequest, TResponse}"/> —
    /// no <c>next</c> parameter, no chain responsibility.
    /// </para>
    /// <para>
    /// Multiple post-processors execute in registration order.
    /// Post-processors are NOT invoked if the handler throws.
    /// </para>
    /// </summary>
    public interface IRequestPostProcessor<in TRequest, in TResponse>
    {
        /// <summary>
        /// Runs after the handler for <paramref name="request"/> has returned successfully.
        /// </summary>
        /// <remarks>
        /// Not invoked when the handler throws. When the work is synchronous, return a completed
        /// value rather than marking the method <c>async</c>.
        /// </remarks>
        /// <param name="request">The request that was handled.</param>
        /// <param name="response">The response the handler produced.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when this post-processor is done.</returns>
        ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken);
    }
}
