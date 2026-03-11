// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Non-generic marker for all commands (write operations in CQRS).
    /// Follows the BCL pattern (<see cref="System.Collections.IEnumerable"/> / <see cref="System.Collections.Generic.IEnumerable{T}"/>).
    /// <para>
    /// Pipeline behaviors can detect commands at runtime:
    /// <code>if (request is ICommand)</code>
    /// </para>
    /// </summary>
    public interface ICommand { };

    /// <summary>
    /// Represents a command that returns <typeparamref name="TResponse"/>.
    /// Commands represent intent to change application state (write operations in CQRS).
    /// For void-returning commands, use <c>ICommand&lt;Unit&gt;</c>.
    /// </summary>
    public interface ICommand<out TResponse> : IRequest<TResponse>, ICommand { };
}


