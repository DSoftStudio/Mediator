// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;

namespace DSoftStudio.Mediator.Abstractions
{
    public interface IStreamRequest<out TResponse> { }

    public interface IStreamRequestHandler<in TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            CancellationToken cancellationToken);
    }
}
