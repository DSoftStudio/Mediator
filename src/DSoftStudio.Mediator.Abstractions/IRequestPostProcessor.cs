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
        ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken);
    }
}
