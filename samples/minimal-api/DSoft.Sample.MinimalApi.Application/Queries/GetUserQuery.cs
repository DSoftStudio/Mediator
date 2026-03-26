// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoft.Sample.MinimalApi.Application.Models;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Application.Queries;

// Recipe 1: Query with route parameter → GET with NotFound
public record GetUserQuery(int Id) : IQuery<UserDto?>;

public class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto?>
{
    // Simulated user store
    private static readonly Dictionary<int, UserDto> Users = new()
    {
        [1] = new UserDto(1, "Alice", "alice@example.com"),
        [2] = new UserDto(2, "Bob", "bob@example.com"),
        [3] = new UserDto(3, "Charlie", "charlie@example.com"),
    };

    public ValueTask<UserDto?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        Users.TryGetValue(request.Id, out var user);
        return new ValueTask<UserDto?>(user);
    }
}
