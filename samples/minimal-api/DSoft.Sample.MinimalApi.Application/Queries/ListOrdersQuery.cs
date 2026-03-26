// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Application.Models;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Application.Queries;

// Recipe 4: Query with pagination → GET with QueryString
public record ListOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null) : IQuery<PagedResult<OrderSummaryDto>>;

public class ListOrdersQueryHandler : IQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>>
{
    // Simulated order store
    private static readonly List<OrderSummaryDto> AllOrders =
    [
        new(1, "CUST-001", "Pending", 150.00m),
        new(2, "CUST-002", "Shipped", 89.99m),
        new(3, "CUST-001", "Delivered", 320.50m),
        new(4, "CUST-003", "Pending", 45.00m),
        new(5, "CUST-002", "Cancelled", 210.00m),
    ];

    public ValueTask<PagedResult<OrderSummaryDto>> Handle(
        ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var filtered = string.IsNullOrEmpty(request.Status)
            ? AllOrders
            : AllOrders.Where(o => o.Status.Equals(request.Status, StringComparison.OrdinalIgnoreCase)).ToList();

        var paged = filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var result = new PagedResult<OrderSummaryDto>(paged, filtered.Count, request.Page, request.PageSize);
        return new ValueTask<PagedResult<OrderSummaryDto>>(result);
    }
}
