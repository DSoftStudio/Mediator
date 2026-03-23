// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Host.Application.Models;

namespace Host.Application.Queries;

/// <summary>
/// A query that looks up a user by ID and returns <c>null</c> when not found.
/// Defined in an Abstractions-only project to exercise the
/// <c>ReferencedAssemblyScanner</c> Phase 2 code path with a nullable response type.
/// </summary>
public sealed record FindUserQuery(string UserId) : IQuery<UserDto?>;

/// <summary>
/// Handler for <see cref="FindUserQuery"/>.
/// Returns <c>null</c> when <c>UserId</c> is <c>"missing"</c>.
/// </summary>
public sealed class FindUserQueryHandler : IQueryHandler<FindUserQuery, UserDto?>
{
    public ValueTask<UserDto?> Handle(FindUserQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == "missing")
            return new ValueTask<UserDto?>((UserDto?)null);

        return new ValueTask<UserDto?>(new UserDto { Name = request.UserId });
    }
}
