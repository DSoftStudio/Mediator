![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

# DSoftStudio.Mediator.HybridCache

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.HybridCache.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.HybridCache)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

HybridCache integration for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). One pipeline behavior routes the requests that opt in through `Microsoft.Extensions.Caching.Hybrid` — multi-layer caching (L1 memory + L2 distributed), stampede prevention and serialization.

## Features

- **Multi-layer caching** — L1 in-memory + L2 distributed, provided by Microsoft HybridCache
- **Stampede prevention** — concurrent callers on one key share a single handler execution
- **Caller-supplied cache keys** — you write the key; it reaches HybridCache verbatim
- **Per-request duration** — `Duration` on the request; 60 seconds when you don't override it
- **Opt-in** — only requests implementing `ICachedRequest` are cached; everything else passes through after one type check

## Installation

```shell
dotnet add package DSoftStudio.Mediator.HybridCache
```

## Quick Start

Implement `ICachedRequest` on the request. `CacheKey` is required; `Duration` is optional and defaults to 60 seconds:

```csharp
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;

public record GetProduct(Guid Id) : IQuery<ProductDto>, ICachedRequest
{
    public string CacheKey => $"GetProduct:{Id}";
    public TimeSpan Duration => TimeSpan.FromMinutes(5);
}

public record ProductDto(Guid Id, string Name, decimal Price);

public sealed class GetProductHandler : IQueryHandler<GetProduct, ProductDto>
{
    public ValueTask<ProductDto> Handle(GetProduct request, CancellationToken cancellationToken)
        => new(new ProductDto(request.Id, "Widget Pro", 29.99m));
}
```

Register at startup. `AddHybridCache()` is Microsoft's own registration and must be present — the behavior takes a `HybridCache` dependency:

```csharp
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.HybridCache;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services
    .AddMediator()
    .RegisterMediatorHandlers();

services.AddHybridCache();

services
    .AddMediatorHybridCache()
    .PrecompilePipelines();
```

On a hit the handler is never invoked — the behavior returns the cached response. `ICachedRequest` works on any request shape: `IQuery<T>`, `ICommand<T>` or plain `IRequest<T>`.

## Registration order

`AddMediatorHybridCache()` registers `CachingBehavior<,>` as an *open* generic. `PrecompilePipelines()` is the step that closes every open-generic behavior over the discovered request/response pairs and builds the pipeline chains, so the call has to sit **after `RegisterMediatorHandlers()` and before `PrecompilePipelines()`**.

Get the order wrong and the call is a no-op: the behavior is never closed, no chain is built for it, and every request goes straight to its handler. Nothing throws — the only symptom is a cache that never hits.

The analyzer reports **DSOFT010** when it can see the mistake, which means both calls in the same method on the same service collection: the Program.cs shape. A companion registered from another method or another assembly is beyond what any analyzer can order, so keep the calls in one chain where the order is visible.


If a request is both cached and validated, registration order decides whether a cache **hit** is
validated: whichever behavior is registered first is the outer one. Register validation before
caching, or every dispatch after the first is served from the entry with its rules skipped.

## Caching one request instead of all of them

`AddMediatorHybridCache()` registers the behavior as an open generic, so it joins the pipeline of **every** request in the application — including the ones that will never cache. Those requests do not pay for the cache itself (the behavior passes them straight through), but a chain now exists where none did, and a request with no pipeline at all is dispatched directly to its handler.

When caching covers a known handful of queries, register those pairs instead:

```csharp
services
    .AddMediatorHybridCache<GetProduct, ProductDto>()
    .AddMediatorHybridCache<GetCustomer, string>()
    .PrecompilePipelines();
```

Every other request keeps its direct dispatch. The type parameter is constrained to `ICachedRequest`, so registering a request that never opted in does not compile — a mistake the open form cannot catch.

The two forms are alternatives, not layers. If the open one has already been registered, the closed call stands down: it would otherwise put the behavior in that chain twice and the outer lookup would re-enter the inner one on the same key.

## Cache keys are yours to namespace

`CacheKey` is passed to `HybridCache` exactly as you return it. Nothing about the request type, its properties or the assembly is mixed in, which means **two request types that return the same key string share one cache entry**: whichever runs first populates it, and the second gets that value back without its own handler ever running.

Put the request type in the key so that cannot happen:

```csharp
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;

// Safe: the type name makes the key unique across request types.
public record GetCustomer(Guid Id) : IQuery<string>, ICachedRequest
{
    public string CacheKey => $"{nameof(GetCustomer)}:{Id}";
}

// Would collide with GetCustomer if both returned "customer:{Id}".
public record GetCustomerSummary(Guid Id) : IQuery<string>, ICachedRequest
{
    public string CacheKey => $"{nameof(GetCustomerSummary)}:{Id}";
}

public sealed class GetCustomerHandler : IQueryHandler<GetCustomer, string>
{
    public ValueTask<string> Handle(GetCustomer request, CancellationToken cancellationToken)
        => new("customer");
}

public sealed class GetCustomerSummaryHandler : IQueryHandler<GetCustomerSummary, string>
{
    public ValueTask<string> Handle(GetCustomerSummary request, CancellationToken cancellationToken)
        => new("summary");
}
```

Every property the response depends on belongs in the key too — a paged query keyed only on its filter serves page 1 to every page.

That includes what the handler reads from **ambient** state rather than from the request. Stampede prevention means concurrent callers on one key share a single handler execution: the first caller's handler runs, and everyone waiting gets its result. If that handler resolves the current tenant, user or culture from `IHttpContextAccessor` or an `AsyncLocal`, the first caller's answer is what the others receive — and what the entry serves for the rest of its lifetime. Put it in the key:

```csharp
public record GetDashboard(Guid Id, string TenantId) : IQuery<DashboardDto>, ICachedRequest
{
    // The tenant is part of what the response depends on, so it is part of the key.
    public string CacheKey => $"{nameof(GetDashboard)}:{TenantId}:{Id}";
}
```

## Invalidation

The behavior only writes entries; removing them is on you. Inject `HybridCache` and remove the same key you built:

```csharp
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;

public record DeleteProduct(Guid Id) : ICommand<Unit>;

public sealed class DeleteProductHandler : ICommandHandler<DeleteProduct, Unit>
{
    private readonly HybridCache _cache;

    public DeleteProductHandler(HybridCache cache) => _cache = cache;

    public async ValueTask<Unit> Handle(DeleteProduct request, CancellationToken cancellationToken)
    {
        // ... delete from the database ...
        await _cache.RemoveAsync($"GetProduct:{request.Id}", cancellationToken);
        return Unit.Value;
    }
}
```

## Adding a distributed L2

`HybridCache` promotes any registered `IDistributedCache` to its second layer, so this is all it takes — no change to requests or handlers. Redis needs its own package:

```shell
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddStackExchangeRedisCache(options =>
    options.Configuration = "localhost:6379");

services.AddHybridCache();
```

## What the behavior does not do

- **No key derivation.** Keys are not generated from the request type or its property values; see above.
- **No invalidation on writes.** A command that changes the data does not evict anything by itself.
- **No entry options beyond duration.** `Duration` becomes `HybridCacheEntryOptions.Expiration`; tags, flags and local-cache expiration stay at HybridCache's defaults.
- **No stream caching.** `CachingBehavior` is an `IPipelineBehavior`, so `CreateStream` requests are untouched.

## Documentation

📖 [Full documentation](https://docs.dsoftstudio.com/mediator/integrations/hybridcache)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
