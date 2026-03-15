# HybridCache Integration

The `DSoftStudio.Mediator.HybridCache` package provides automatic request caching via Microsoft's [`HybridCache`](https://learn.microsoft.com/dotnet/core/extensions/hybrid-cache) — multi-layer caching (L1 memory + L2 distributed), stampede prevention, and serialization out of the box.

## Installation

```shell
dotnet add package DSoftStudio.Mediator.HybridCache
```

## Registration

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddHybridCache()                   // Microsoft's built-in registration
    .AddMediatorHybridCache()           // Registers CachingBehavior<,>
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

## Mark Requests as Cacheable

Implement `ICachedRequest` on any request to opt-in to caching:

```csharp
public record GetProduct(Guid Id) : IQuery<ProductDto>, ICachedRequest
{
    public string CacheKey => $"products:{Id}";
    public TimeSpan Duration => TimeSpan.FromMinutes(5);  // default: 60s
}
```

That's it — the `CachingBehavior` intercepts the pipeline, checks for `ICachedRequest`, and uses `HybridCache.GetOrCreateAsync()` to cache the result. Requests that don't implement `ICachedRequest` pass through with zero overhead.

## Adding Redis as L2

```csharp
services.AddStackExchangeRedisCache(options =>
    options.Configuration = "localhost:6379");
```

`HybridCache` automatically uses the registered `IDistributedCache` as L2 — no changes to your mediator code.

## Cache Invalidation

Use `HybridCache.RemoveAsync()` directly in command handlers:

```csharp
public class DeleteProductHandler : ICommandHandler<DeleteProduct, Unit>
{
    private readonly HybridCache _cache;

    public DeleteProductHandler(HybridCache cache) => _cache = cache;

    public async ValueTask<Unit> Handle(DeleteProduct request, CancellationToken ct)
    {
        // ... delete from database ...
        await _cache.RemoveAsync($"products:{request.Id}", ct);
        return Unit.Value;
    }
}
```

## Behavior Summary

| Scenario | Result |
|---|---|
| Request implements `ICachedRequest` | Result cached via `HybridCache` with specified duration |
| Request does not implement `ICachedRequest` | Pass-through — handler executes normally |
| Same cache key within TTL | Cached result returned, handler not invoked |
| Concurrent requests for same key | Stampede prevention — one execution, all callers share result |
