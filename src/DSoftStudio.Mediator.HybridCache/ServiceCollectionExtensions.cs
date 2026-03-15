// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache;

public static class HybridCacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="CachingBehavior{TRequest,TResponse}"/> as an open-generic
    /// pipeline behavior. Requests that implement <see cref="ICachedRequest"/> will have
    /// their results cached via <c>HybridCache</c>; all other requests pass through.
    /// <para>
    /// Requires <c>AddHybridCache()</c> to be called before or after this method.
    /// Call this after <c>AddMediator()</c> / <c>RegisterMediatorHandlers()</c>
    /// and before <c>PrecompilePipelines()</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// services
    ///     .AddMediator()
    ///     .RegisterMediatorHandlers()
    ///     .AddHybridCache()
    ///     .AddMediatorHybridCache()
    ///     .PrecompilePipelines();
    /// </code>
    /// </example>
    public static IServiceCollection AddMediatorHybridCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        return services;
    }
}
