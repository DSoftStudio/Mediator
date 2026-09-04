---
layout: default
title: "HybridCache - DSoftStudio.Mediator"
description: "Transparent query caching with Microsoft HybridCache."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

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

That's it — the `CachingBehavior` intercepts the pipeline, checks for `ICachedRequest`, and uses `HybridCache.GetOrCreateAsync()` to cache the result. Requests that don't implement `ICachedRequest` are passed straight through by a single `is not ICachedRequest` type-check.

That check is not the whole cost, though. `AddMediatorHybridCache()` registers the behavior as an **open** generic, so it joins the pipeline of every request in the application, and a request that would otherwise have no pipeline components at all now has a chain built for it instead of being dispatched directly to its handler. Register [per request pair](#caching-one-request-instead-of-all-of-them) when caching covers a known handful of queries, and every other request keeps its direct dispatch.

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

## Caching One Request Instead of All of Them

The generic overload registers the behavior closed over one (request, response) pair, so only that pair gets a pipeline chain:

```csharp
services
    .AddMediatorHybridCache<GetProduct, ProductDto>()
    .PrecompilePipelines();
```

`TRequest` is constrained to `ICachedRequest`, so registering a request that never opted in does not compile — a mistake the open form cannot catch.

The two forms are alternatives, not layers. If the open registration is already present the closed call stands down, because a second descriptor would put the behavior in that chain twice and the outer lookup would re-enter the inner one on the same key.

## What the Cache Key Must Contain

`CacheKey` reaches `HybridCache` verbatim. Nothing about the request type or its properties is mixed in, so two request types returning the same string share one entry — put the request type in the key.

Everything the response depends on belongs there too, **including what the handler reads from ambient state rather than from the request**. Stampede prevention means concurrent callers on one key share a single handler execution: the first caller's handler runs and everyone waiting receives its result. A handler that resolves the current tenant or user from `IHttpContextAccessor` or an `AsyncLocal` therefore serves the first caller's answer to the rest, for the lifetime of the entry.

```csharp
public record GetDashboard(Guid Id, string TenantId) : IQuery<DashboardDto>, ICachedRequest
{
    // The tenant is part of what the response depends on, so it is part of the key.
    public string CacheKey => $"{nameof(GetDashboard)}:{TenantId}:{Id}";
}
```

## Order Against Validation

Pipeline behaviors run in registration order, and when a request is both cached and validated that
order decides whether a cache hit is validated at all.

```csharp
// Validation outer: every dispatch is validated, hits included.
services.AddMediatorFluentValidation();
services.AddMediatorHybridCache();

// Caching outer: a hit returns before validation is ever reached.
services.AddMediatorHybridCache();
services.AddMediatorFluentValidation();
```

With caching registered first, the first dispatch populates the entry and every later one is served
from it — **without running the validators**. Nothing warns; the rules are simply skipped. If
validation is authorization, a permission check, or anything else that can change its answer between
two calls with the same key, register **validation before caching**.

An invalid request never populates the cache in either order: the handler does not run, so there is
no value to store, and the next dispatch is rejected again rather than served a cached failure.

## Behavior Summary

| Scenario | Result |
|---|---|
| Request implements `ICachedRequest` | Result cached via `HybridCache` with specified duration |
| Request does not implement `ICachedRequest` | Pass-through — handler executes normally |
| Same cache key within TTL | Cached result returned, handler not invoked |
| Concurrent requests for same key | Stampede prevention — one execution, all callers share result |
| Registered open-generic | Every request in the application gets a pipeline chain |
| Registered per pair | Only that pair gets a chain; every other request keeps its direct dispatch |
| Registered before validation | A cache hit is served without validating the request |

## See Also

- [Caching Patterns](../advanced/caching-patterns.md) — manual caching approach with `IQuery<T>` constraint
- [CQRS](../concepts/cqrs.md) — use `IQuery<T>` marker for read-only operations
- [Pipeline Behaviors](../features/pipeline-behaviors.md) — how `CachingBehavior` wraps the pipeline
