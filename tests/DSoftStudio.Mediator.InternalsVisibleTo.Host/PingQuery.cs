// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.InternalsVisibleTo.Host;

/// <summary>
/// Simple query to reproduce the CS0436 scenario.
/// The source generator will produce MediatorServiceRegistry, MediatorHandlerValidator, etc.
/// in this assembly. With InternalsVisibleTo, those internal types leak to the test project.
/// </summary>
public record PingQuery : IQuery<string>;

public sealed class PingQueryHandler : IQueryHandler<PingQuery, string>
{
    public ValueTask<string> Handle(PingQuery request, CancellationToken cancellationToken)
        => new("Pong");
}
