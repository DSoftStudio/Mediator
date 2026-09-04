// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache.Tests.Fixtures;

internal static class TestServiceProvider
{
    /// <summary>
    /// Builds a fully configured service provider with mediator + HybridCache + caching behavior.
    /// Optionally configures additional services via <paramref name="configure"/>.
    /// </summary>
    /// <param name="configureTwice">Calls <c>AddMediatorHybridCache()</c> a second time, standing in
    /// for a composition root that registers it from two modules — the behavior must not end up
    /// nested inside itself.</param>
    public static IServiceProvider Build(
        Action<IServiceCollection>? configure = null,
        bool configureTwice = false)
    {
        var services = new ServiceCollection();

        services
            .AddMediator()
            .RegisterMediatorHandlers();

        services.AddHybridCache();

        configure?.Invoke(services);

        services.AddMediatorHybridCache();

        if (configureTwice)
            services.AddMediatorHybridCache();

        services
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        return services.BuildServiceProvider();
    }
}
