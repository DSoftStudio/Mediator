// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;

namespace DSoft.Sample.HybridCache.Application.Queries;

// ── Request ──────────────────────────────────────────────────────────

public record GetProductQuery(Guid Id) : IQuery<ProductDto>, ICachedRequest
{
    public string CacheKey => $"products:{Id}";
    public TimeSpan Duration => TimeSpan.FromMinutes(5);
}

public record ProductDto(Guid Id, string Name, decimal Price);

// ── Handler ──────────────────────────────────────────────────────────

public sealed class GetProductHandler : IQueryHandler<GetProductQuery, ProductDto>
{
    public ValueTask<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Handler] Fetching product {request.Id} from database...");
        return new(new ProductDto(request.Id, "Widget Pro", 29.99m));
    }
}
