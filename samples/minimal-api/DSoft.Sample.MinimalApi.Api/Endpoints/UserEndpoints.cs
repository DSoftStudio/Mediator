// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Application.Models;
using DSoft.Sample.MinimalApi.Application.Queries;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users");

        // Recipe 1: Query with route parameter → GET with NotFound
        group.MapGet("/{id}", async (int id, ISender sender, CancellationToken ct) =>
            await sender.Send(new GetUserQuery(id), ct) is { } user
                ? Results.Ok(user)
                : Results.NotFound())
            .WithName("GetUser")
            .Produces<UserDto>(200)
            .ProducesProblem(404);
    }
}
