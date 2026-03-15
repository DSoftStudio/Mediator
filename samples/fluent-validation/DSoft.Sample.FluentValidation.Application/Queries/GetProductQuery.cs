// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.FluentValidation.Application.Queries;

public record GetProductQuery(Guid Id) : IQuery<ProductDto>;

public record ProductDto(Guid Id, string Name, decimal Price);

public sealed class GetProductQueryHandler : IQueryHandler<GetProductQuery, ProductDto>
{
    public ValueTask<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
        => new(new ProductDto(request.Id, "Sample Product", 29.99m));
}
