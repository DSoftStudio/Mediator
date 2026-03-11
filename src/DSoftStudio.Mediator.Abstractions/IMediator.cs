// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Central dispatcher for requests, notifications, and streams.
    /// <para>
    /// Extends <see cref="ISender"/> and <see cref="IPublisher"/> — prefer injecting
    /// those narrower interfaces when a component only needs one capability.
    /// </para>
    /// </summary>
    public interface IMediator : ISender, IPublisher
    {
        /// <summary>
        /// Creates an async stream using static generic dispatch — no dictionary lookup,
        /// no wrapper allocation.
        /// </summary>
        IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IStreamRequest<TResponse>;
    }

}


