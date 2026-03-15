// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.OpenTelemetry.Application.Queries;

public record GetOrdersQuery(int Count = 5) : IQuery<List<OrderDto>>;

public sealed class GetOrdersQueryHandler : IQueryHandler<GetOrdersQuery, List<OrderDto>>
{
    public async ValueTask<List<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        return Enumerable.Range(1, request.Count)
            .Select(i => new OrderDto(Guid.NewGuid(), $"Product-{i}", i))
            .ToList();
    }
}

public record OrderDto(Guid Id, string Product, int Quantity);
