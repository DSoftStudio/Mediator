// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DSoftStudio.Mediator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ADR-0065 AGGRESSIVE tier safety net: every container passes through here (the generated
        // AddMediator(configure) overload calls this too, and no IMediator exists without it).
        // A second distinct collection permanently poisons the static-holder fast path so it can
        // never serve one container's Singleton to another — dispatch degrades to the SAFE tier.
        AggressiveDispatchLatch.OnContainerRegistered(services);

        // Per-container lifetime snapshot for the provider-keyed dispatch caches. Registered as an
        // instance so it captures THIS collection; read lazily on first dispatch, by which time the
        // descriptor list is final. Every container reaches here, so every container gets its own.
        services.TryAddSingleton(new DispatchLifetimeMap(services));

        services.TryAddScoped<IMediator, Mediator>();
        services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        return services;
    }
}

