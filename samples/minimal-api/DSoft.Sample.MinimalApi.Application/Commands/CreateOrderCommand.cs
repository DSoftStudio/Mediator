// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Application.Models;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Application.Commands;

// Recipe 2: Command with body → POST with Created
public record CreateOrderCommand(string CustomerId, List<OrderItem> Items) : ICommand<OrderId>;

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderId>
{
    private static int _nextId = 100;

    public ValueTask<OrderId> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Simulate order creation
        var id = Interlocked.Increment(ref _nextId);
        return new ValueTask<OrderId>(new OrderId(id));
    }
}
