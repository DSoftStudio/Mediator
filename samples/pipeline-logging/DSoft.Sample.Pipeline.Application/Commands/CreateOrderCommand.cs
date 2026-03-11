// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Pipeline.Application.Commands;

public record CreateOrderCommand(string ProductName, int Quantity) : ICommand<Guid>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Simulate order creation
        var orderId = Guid.NewGuid();
        return new(orderId);
    }
}
