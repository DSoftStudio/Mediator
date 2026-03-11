// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Sends requests through the mediator pipeline.
    /// <para>
    /// Use this interface when a component only needs to send requests (commands/queries)
    /// without publishing notifications. Follows the Interface Segregation Principle.
    /// </para>
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Sends a request through the pipeline and returns the handler's response.
        /// Uses static generic dispatch — no dictionary lookup, no wrapper allocation.
        /// </summary>
        ValueTask<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>;
    }
}
