// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.ModularMonolith.Module;

/// <summary>
/// Public contract + public handler — should be discovered and registered
/// by the host's source generator via Phase 2 (type-based scanning).
/// </summary>
public sealed record GetTemperatureQuery : IQuery<int>;

public sealed class GetTemperatureQueryHandler : IQueryHandler<GetTemperatureQuery, int>
{
    public ValueTask<int> Handle(GetTemperatureQuery request, CancellationToken cancellationToken)
        => new(25);
}
