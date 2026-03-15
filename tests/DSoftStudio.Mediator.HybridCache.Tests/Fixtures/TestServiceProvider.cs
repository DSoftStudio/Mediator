// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache.Tests;

internal static class TestServiceProvider
{
    /// <summary>
    /// Builds a fully configured service provider with mediator + HybridCache + caching behavior.
    /// Optionally configures additional services via <paramref name="configure"/>.
    /// </summary>
    public static IServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services
            .AddMediator()
            .RegisterMediatorHandlers();

        services.AddHybridCache();

        configure?.Invoke(services);

        services
            .AddMediatorHybridCache()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        return services.BuildServiceProvider();
    }
}
