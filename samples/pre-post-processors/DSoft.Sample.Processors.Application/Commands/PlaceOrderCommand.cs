// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Processors.Application.Commands;

public record PlaceOrderCommand(string ProductName, int Quantity) : ICommand<Guid>;

public sealed class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, Guid>
{
    public ValueTask<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        return new(orderId);
    }
}
