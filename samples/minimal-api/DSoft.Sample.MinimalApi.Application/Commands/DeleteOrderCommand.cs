// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Application.Commands;

// Recipe 3: Void command → DELETE with NoContent
public record DeleteOrderCommand(int Id) : ICommand<Unit>;

public class DeleteOrderCommandHandler : ICommandHandler<DeleteOrderCommand, Unit>
{
    public ValueTask<Unit> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        // Simulate order deletion
        return new ValueTask<Unit>(Unit.Value);
    }
}
