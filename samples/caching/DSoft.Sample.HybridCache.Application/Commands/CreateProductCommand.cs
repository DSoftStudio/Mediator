// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.HybridCache.Application.Commands;

// ── Request ──────────────────────────────────────────────────────────

public record CreateProductCommand(string Name, decimal Price) : ICommand<Guid>;

// ── Handler (not cached — commands mutate state) ─────────────────────

public sealed class CreateProductHandler : ICommandHandler<CreateProductCommand, Guid>
{
    public ValueTask<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        Console.WriteLine($"[Handler] Created product '{request.Name}' with id {id}");
        return new(id);
    }
}
