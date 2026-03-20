// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace Host.Application.Commands;

/// <summary>
/// A command that creates a new order and returns the order ID.
/// Defined in an Abstractions-only project — no source generators here.
/// </summary>
public sealed record CreateOrderCommand(string ProductName, int Quantity) : ICommand<int>;

/// <summary>
/// Handler for <see cref="CreateOrderCommand"/>.
/// Discovered by the generator in the Host project via
/// <c>ReferencedAssemblyScanner</c> Phase 2 (type-based fallback).
/// </summary>
public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, int>
{
    public ValueTask<int> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        => new(1001); // Simulated order ID
}
