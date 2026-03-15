---
layout: default
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Advanced Patterns

## Query Caching with HybridCache

For automatic query caching, use the [`DSoftStudio.Mediator.HybridCache`](../integrations/hybridcache.md) companion package — it provides `ICachedRequest`, `CachingBehavior`, and `AddMediatorHybridCache()` out of the box.

If you prefer a manual approach without the package dependency, you can write a custom caching behavior in ~30 lines. The pattern below constrains the behavior to queries only (via `IQuery<TResponse>`):

```csharp
public interface ICachedQuery
{
    string CacheKey => $"{GetType().Name}:{this}";
    static virtual TimeSpan Duration => TimeSpan.FromSeconds(60);
}

public sealed class QueryCacheBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>, ICachedQuery
{
    private readonly HybridCache _cache;

    public QueryCacheBehavior(HybridCache cache) => _cache = cache;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(
            request.CacheKey,
            async token => await next.Handle(request, token),
            new HybridCacheEntryOptions { Expiration = TRequest.Duration },
            cancellationToken: ct);
    }
}

// Registration
services.AddHybridCache();
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCacheBehavior<,>));
```
