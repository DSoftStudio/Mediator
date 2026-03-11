// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Handles a command of type <typeparamref name="TCommand"/> and returns <typeparamref name="TResponse"/>.
    /// Commands represent write operations in CQRS — they modify application state.
    /// <para>
    /// This is a semantic alias for <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// The mediator pipeline, DI registration, and source generators treat it identically
    /// to <c>IRequestHandler</c> — zero additional runtime cost.
    /// </para>
    /// </summary>
    public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
    }

    /// <summary>
    /// Handles a command of type <typeparamref name="TCommand"/> that returns no meaningful result.
    /// Convenience alias for <c>ICommandHandler&lt;TCommand, Unit&gt;</c>.
    /// </summary>
    public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
        where TCommand : ICommand<Unit>
    {
    }
}
