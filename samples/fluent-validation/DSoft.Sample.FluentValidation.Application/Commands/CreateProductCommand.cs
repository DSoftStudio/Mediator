// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using FluentValidation;

namespace DSoft.Sample.FluentValidation.Application.Commands;

public record CreateProductCommand(string Name, decimal Price) : ICommand<Guid>;

public sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    public ValueTask<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        => new(Guid.NewGuid());
}

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(100).WithMessage("Product name must not exceed 100 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}
