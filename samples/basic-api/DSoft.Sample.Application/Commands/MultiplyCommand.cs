// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Application.Commands;

public record MultiplyCommand(int A, int B) : ICommand<int>;

public sealed class MultiplyCommandHandler : ICommandHandler<MultiplyCommand, int>
{
    public ValueTask<int> Handle(MultiplyCommand request, CancellationToken cancellationToken)
        => new(request.A * request.B);
}
