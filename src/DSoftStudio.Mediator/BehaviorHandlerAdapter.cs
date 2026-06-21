// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Adapts an <see cref="IPipelineBehavior{TRequest, TResponse}"/> + next handler
    /// into an <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// Used by the reentrant fallback path and by <see cref="PipelineBuilder"/>.
    /// </summary>
    internal sealed class BehaviorHandlerAdapter<TRequest, TResponse>(
        IPipelineBehavior<TRequest, TResponse> behavior,
        IRequestHandler<TRequest, TResponse> next)
        : IRequestHandler<TRequest, TResponse>, IPipelineHandlerTypeAccessor
        where TRequest : IRequest<TResponse>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
            => behavior.Handle(request, next, cancellationToken);

        /// <summary>
        /// Walks the chain to the terminal handler: an inner adapter forwards its own resolution; the tail
        /// (the concrete handler, which does not implement the accessor) reports its runtime type. Lets an
        /// outermost behavior tag the concrete handler without resolving it (<see cref="IPipelineHandlerTypeAccessor"/>).
        /// </summary>
        public Type HandlerType
            => next is IPipelineHandlerTypeAccessor inner ? inner.HandlerType : next.GetType();
    }
}
