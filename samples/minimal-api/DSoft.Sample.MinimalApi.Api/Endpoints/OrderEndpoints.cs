// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Application.Commands;
using DSoft.Sample.MinimalApi.Application.Models;
using DSoft.Sample.MinimalApi.Application.Queries;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders");

        // Recipe 4: Query with pagination → GET with QueryString
        group.MapGet("/", async ([AsParameters] ListOrdersQuery query, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .WithName("ListOrders")
            .Produces<PagedResult<OrderSummaryDto>>(200);

        // Recipe 2: Command with body → POST with Created
        group.MapPost("/", async (CreateOrderCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/orders/{result.Value}", result);
        })
            .WithName("CreateOrder")
            .Produces<OrderId>(201);

        // Recipe 3: Void command → DELETE with NoContent
        group.MapDelete("/{id}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteOrderCommand(id), ct);
            return Results.NoContent();
        })
            .WithName("DeleteOrder")
            .Produces(204);

        // Recipe 3: Void command → PUT with NoContent
        group.MapPut("/{id}/cancel", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelOrderCommand(id), ct);
            return Results.NoContent();
        })
            .WithName("CancelOrder")
            .Produces(204);

        // Recipe 5: Command with authorization → POST with RequireAuthorization
        group.MapPost("/{orderId}/refund", async (int orderId, RefundOrderCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd with { OrderId = orderId }, ct);
            return Results.Ok(result);
        })
            .RequireAuthorization("ManagerPolicy")
            .WithName("RefundOrder")
            .Produces<RefundResult>(200)
            .ProducesProblem(403);
    }
}
