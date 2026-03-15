// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;
using Cache = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace DSoftStudio.Mediator.HybridCache;

/// <summary>
/// Pipeline behavior that caches results for requests implementing <see cref="ICachedRequest"/>.
/// <para>
/// Uses <c>HybridCache</c> (L1 memory + optional L2 distributed) with built-in
/// stampede prevention and serialization. When the request does not implement
/// <see cref="ICachedRequest"/>, the behavior is a no-op pass-through.
/// </para>
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Cache _cache;

    public CachingBehavior(Cache cache)
    {
        _cache = cache;
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICachedRequest cached)
            return await next.Handle(request, cancellationToken);

        return await _cache.GetOrCreateAsync(
            cached.CacheKey,
            async token => await next.Handle(request, token),
            new HybridCacheEntryOptions
            {
                Expiration = cached.Duration
            },
            cancellationToken: cancellationToken);
    }
}
