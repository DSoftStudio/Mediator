<p align="center">
  <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="200">
</p>

# DSoftStudio.Mediator.HybridCache

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.HybridCache.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.HybridCache)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

HybridCache integration for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). Provides automatic query caching via a pipeline behavior that leverages `Microsoft.Extensions.Caching.Hybrid` for multi-layer caching (L1 memory + L2 distributed).

## Features

- **Multi-layer caching** — L1 in-memory + L2 distributed cache via Microsoft HybridCache
- **Stampede prevention** — Built-in protection against cache stampede (thundering herd)
- **Automatic cache keys** — Generated from request type and property values
- **Configurable TTL** — Per-request expiration via `ICachedRequest<T>`
- **Opt-in design** — Only requests implementing `ICachedRequest<T>` are cached

## Installation

```shell
dotnet add package DSoftStudio.Mediator.HybridCache
```

## Quick Start

Mark a request as cacheable:

```csharp
public record GetProduct(int Id) : IQuery<ProductDto>, ICachedRequest<ProductDto>
{
    public TimeSpan Expiration => TimeSpan.FromMinutes(5);
}
```

Register at startup:

```csharp
services.AddHybridCache();

services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorCaching()
    .PrecompilePipelines();
```

Cached responses are returned automatically on subsequent calls — the handler only executes on cache misses.

## Documentation

📖 [Full documentation](https://github.com/DSoftStudio/Mediator/blob/main/docs/integrations/hybridcache.md)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
