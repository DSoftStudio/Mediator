// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Pipeline behavior that wraps the execution of a request handler.
    /// Behaviors execute in registration order, forming a chain:
    /// <c>Behavior1 → Behavior2 → … → Handler</c>.
    /// <para>
    /// The <paramref name="next"/> parameter is an <see cref="IRequestHandler{TRequest, TResponse}"/>
    /// representing the next step in the pipeline (another behavior or the terminal handler).
    /// Call <c>next.Handle(request, cancellationToken)</c> to continue the chain.
    /// This uses virtual dispatch instead of delegate invocation for maximum performance.
    /// </para>
    /// Use for cross-cutting concerns: logging, validation, authorization, transactions, etc.
    /// </summary>
    public interface IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken cancellationToken);
    }
}


