// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.InternalsVisibleTo.Tests;

/// <summary>
/// Handler defined in the TEST project.
/// The generator runs here too → produces its own MediatorServiceRegistry, etc.
/// With InternalsVisibleTo from Host, the test project sees BOTH sets of internal types.
/// Before the fix: CS0436 "type conflicts with imported type".
/// After the fix (file modifier): zero conflicts.
/// </summary>
public record EchoQuery(string Message) : IQuery<string>;

public sealed class EchoQueryHandler : IQueryHandler<EchoQuery, string>
{
    public ValueTask<string> Handle(EchoQuery request, CancellationToken cancellationToken)
        => new(request.Message);
}
