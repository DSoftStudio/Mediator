// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator.HybridCache;

/// <summary>
/// Marker interface for requests whose results should be cached via <c>HybridCache</c>.
/// <para>
/// Implement on any <see cref="Abstractions.IRequest{TResponse}"/>,
/// <see cref="Abstractions.IQuery{TResponse}"/>, or <see cref="Abstractions.ICommand{TResponse}"/>
/// to opt-in to automatic caching through <see cref="CachingBehavior{TRequest,TResponse}"/>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public record GetProduct(Guid Id) : IQuery&lt;ProductDto&gt;, ICachedRequest
/// {
///     public string CacheKey => $"products:{Id}";
///     public TimeSpan Duration => TimeSpan.FromMinutes(5);
/// }
/// </code>
/// </example>
public interface ICachedRequest
{
    /// <summary>
    /// The cache key for this request. Must uniquely identify the cached result.
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Cache duration. Default: 60 seconds.
    /// Override to customize per request type.
    /// </summary>
    TimeSpan Duration => TimeSpan.FromSeconds(60);
}
