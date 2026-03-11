// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Processors.Application.Queries;

public record GetOrderQuery(Guid Id) : IQuery<OrderDto>;

public record OrderDto(Guid Id, string ProductName, int Quantity);

public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto>
{
    public ValueTask<OrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = new OrderDto(request.Id, "Sample Product", 1);
        return new(order);
    }
}
