// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Adapts an <see cref="IPipelineBehavior{TRequest, TResponse}"/> + next handler
    /// into an <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// Used by the reentrant fallback path and by <see cref="PipelineBuilder"/>.
    /// </summary>
    internal sealed class BehaviorHandlerAdapter<TRequest, TResponse>(
        IPipelineBehavior<TRequest, TResponse> behavior,
        IRequestHandler<TRequest, TResponse> next) : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
            => behavior.Handle(request, next, cancellationToken);
    }
}
