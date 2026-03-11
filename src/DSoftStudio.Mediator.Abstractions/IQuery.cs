// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.Abstractions
{

    /// <summary>
    /// Non-generic marker for all queries (read-only operations in CQRS).
    /// Enables runtime type-checking (<c>request is IQuery</c>) in pipeline behaviors
    /// without requiring an open-generic pattern match.
    /// Follows the <see cref="System.Collections.IEnumerable"/> / <see cref="System.Collections.Generic.IEnumerable{T}"/> BCL pattern.
    /// </summary>
    public interface IQuery { };

    /// <summary>
    /// Marker interface for a query that returns <typeparamref name="TResponse"/>.
    /// Queries represent read operations in CQRS — they must not modify state.
    /// </summary>
    public interface IQuery<out TResponse> : IRequest<TResponse>, IQuery { };

}

