// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions {
    /// <summary>
    /// Handles a request of type <typeparamref name="TRequest"/> and returns <typeparamref name="TResponse"/>.
    /// </summary>
    /// 
    public interface IRequestHandler<in TRequest, TResponse>
      where TRequest : IRequest<TResponse>
    {
        ValueTask<TResponse> Handle(
            TRequest request,
            CancellationToken cancellationToken);
    }

}


