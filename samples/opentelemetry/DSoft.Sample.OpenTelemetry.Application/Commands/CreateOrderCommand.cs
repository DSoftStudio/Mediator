// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.OpenTelemetry.Application.Commands;

public record CreateOrderCommand(string Product, int Quantity) : ICommand<Guid>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Simulate some work
        await Task.Delay(10, cancellationToken);
        return Guid.NewGuid();
    }
}
