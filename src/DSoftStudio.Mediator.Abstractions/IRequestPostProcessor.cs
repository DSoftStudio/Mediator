// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Runs after the request pipeline has produced a response.
    /// <para>
    /// Use for cross-cutting concerns that only need an "after" hook:
    /// audit logging, caching responses, metrics collection.
    /// Simpler than <see cref="IPipelineBehavior{TRequest, TResponse}"/> —
    /// no <c>next</c> parameter, no chain responsibility.
    /// </para>
    /// <para>
    /// Multiple post-processors execute in registration order. They are skipped whenever the
    /// dispatch fails: a throw from a pre-processor, from a behavior or from the handler bypasses
    /// them all.
    /// </para>
    /// <para>
    /// They DO run in two cases where the handler itself never returned: when an
    /// <see cref="IRequestExceptionHandler{TRequest, TResponse}"/> suppresses the exception, and
    /// when a behavior short-circuits the chain. In both cases they receive the substituted
    /// response, so an auditing post-processor still fires on a rejected or cached result.
    /// </para>
    /// </summary>
    public interface IRequestPostProcessor<in TRequest, in TResponse>
    {
        /// <summary>
        /// Runs after the handler for <paramref name="request"/> has returned successfully.
        /// </summary>
        /// <remarks>
        /// Skipped when the dispatch throws, but still invoked when an exception handler suppresses
        /// the exception or a behavior short-circuits the chain. If a post-processor throws, the
        /// remaining ones are skipped and that exception replaces the response — it is NOT offered
        /// to the exception handlers, whose guard stops before this stage. When the work is
        /// synchronous, return a completed value rather than marking the method <c>async</c>.
        /// </remarks>
        /// <param name="request">The request that was handled.</param>
        /// <param name="response">The response the handler produced.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>A task that completes when this post-processor is done.</returns>
        ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken);
    }
}
