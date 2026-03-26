// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.MinimalApi.Application.Commands;

// Recipe 3: Void command → PUT with NoContent
public record CancelOrderCommand(int Id) : ICommand<Unit>;

public class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, Unit>
{
    public ValueTask<Unit> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        // Simulate order cancellation
        return new ValueTask<Unit>(Unit.Value);
    }
}
