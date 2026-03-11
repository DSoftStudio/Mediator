// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles a query of type <typeparamref name="TQuery"/> and returns <typeparamref name="TResponse"/>.
    /// Queries represent read-only operations in CQRS — they must not modify state.
    /// <para>
    /// This is a semantic alias for <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// The mediator pipeline, DI registration, and source generators treat it identically
    /// to <c>IRequestHandler</c> — zero additional runtime cost.
    /// </para>
    /// </summary>
    public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
    }
}
