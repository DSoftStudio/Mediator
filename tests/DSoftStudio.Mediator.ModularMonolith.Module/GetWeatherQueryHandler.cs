// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.ModularMonolith.Module;

/// <summary>
/// INTERNAL handler — implementation detail of this module.
/// This is the pattern that triggers CS0122 when the host project's
/// source generator discovers this type via Phase 2 (type-based scanning)
/// and tries to emit DI registration code referencing it.
/// </summary>
internal sealed class GetWeatherQueryHandler : IQueryHandler<GetWeatherQuery, string>
{
    public ValueTask<string> Handle(GetWeatherQuery request, CancellationToken cancellationToken)
        => new("Sunny, 25°C");
}
