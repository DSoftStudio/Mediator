// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace Host.Application.Queries;

/// <summary>
/// A query that retrieves an order by ID.
/// Defined in an Abstractions-only project.
/// </summary>
public sealed record GetOrderQuery(int OrderId) : IQuery<string>;

/// <summary>
/// Handler for <see cref="GetOrderQuery"/>.
/// </summary>
public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
{
    public ValueTask<string> Handle(GetOrderQuery request, CancellationToken cancellationToken)
        => new($"Order #{request.OrderId}: Widget × 5");
}
