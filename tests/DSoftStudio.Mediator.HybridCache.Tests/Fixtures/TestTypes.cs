// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;

namespace DSoftStudio.Mediator.HybridCache.Tests.Fixtures;

// ── Cached query ──────────────────────────────────────────────────────

public record GetProduct(Guid Id) : IQuery<ProductDto>, ICachedRequest
{
    public string CacheKey => $"products:{Id}";
}

public record ProductDto(Guid Id, string Name, decimal Price);

public sealed class GetProductHandler : IRequestHandler<GetProduct, ProductDto>
{
    private int _callCount;

    public int CallCount => _callCount;

    public ValueTask<ProductDto> Handle(GetProduct request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return new(new ProductDto(request.Id, "Widget", 9.99m));
    }
}

// ── Cached query with custom duration ─────────────────────────────────

public record GetOrder(int OrderId) : IQuery<string>, ICachedRequest
{
    public string CacheKey => $"orders:{OrderId}";
    public TimeSpan Duration => TimeSpan.FromMinutes(10);
}

public sealed class GetOrderHandler : IRequestHandler<GetOrder, string>
{
    private int _callCount;

    public int CallCount => _callCount;

    public ValueTask<string> Handle(GetOrder request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return new($"order:{request.OrderId}");
    }
}

// ── Non-cached request (pass-through) ─────────────────────────────────

public record Ping(int Value) : IRequest<int>;

public sealed class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken cancellationToken)
        => new(request.Value * 2);
}

// ── Cached command (to prove it works with commands too) ──────────────

public record GetCachedCount : ICommand<int>, ICachedRequest
{
    public string CacheKey => "cached-count";
}

public sealed class GetCachedCountHandler : IRequestHandler<GetCachedCount, int>
{
    private static int s_counter;

    public ValueTask<int> Handle(GetCachedCount request, CancellationToken cancellationToken)
        => new(Interlocked.Increment(ref s_counter));
}

// ── Ambient-state probe ───────────────────────────────────────────────

/// <summary>
/// Stand-in for the ambient state a real handler reads — <c>Activity.Current</c>,
/// <c>IHttpContextAccessor.HttpContext</c>, a tenant/user <see cref="AsyncLocal{T}"/>. All of it
/// rides on the caller's <see cref="ExecutionContext"/>, so one AsyncLocal is the smallest
/// faithful observer of whether that context reached the handler through the cache factory.
/// </summary>
public static class AmbientProbe
{
    public static readonly AsyncLocal<string?> Current = new();

    /// <summary>
    /// Non-null stand-in for "the handler saw no ambient state" — a null response would make
    /// HybridCache serialize a null payload, which is a different question than the one asked.
    /// </summary>
    public const string Missing = "<none>";
}

public record GetAmbient(string Scope) : IQuery<string>, ICachedRequest
{
    public string CacheKey => $"ambient:{Scope}";
}

public sealed class GetAmbientHandler : IRequestHandler<GetAmbient, string>
{
    public ValueTask<string> Handle(GetAmbient request, CancellationToken cancellationToken)
        => new(AmbientProbe.Current.Value ?? AmbientProbe.Missing);
}
