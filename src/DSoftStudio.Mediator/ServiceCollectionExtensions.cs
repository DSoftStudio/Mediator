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

        // A Singleton TYPE, not an instance: the container builds one per provider and shares it with
        // every scope, so each container captures its own answer instead of racing its siblings for
        // the one snapshot the collection-scoped map holds.
        services.TryAddSingleton<DispatchLifetimeSnapshot>();

        // Scoped, and resolved by Mediator's constructor so it exists in every scope that dispatches.
        // Its disposal is the signal that lets the dispatch caches drop this scope: a [ThreadStatic]
        // cannot be written by another thread, and the thread that filled a slot is rarely the one
        // that disposes the scope.
        services.TryAddScoped<MediatorScopeRelease>();

        services.TryAddScoped<IMediator, Mediator>();
        services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        return services;
    }
}

/// <summary>
/// Empties the provider-keyed dispatch cache slots belonging to a scope when that scope is disposed.
/// <para>
/// Registered Scoped and instantiated from <see cref="Mediator"/>'s constructor, so every scope that
/// dispatches gets one and the container disposes it with the scope. Without it a slot keeps the
/// disposed scope — and every scoped instance it resolved — reachable until the thread that filled
/// the slot happens to dispatch the same request type again against a different provider, which for a
/// rarely-used request type or a pool thread that moves on is never.
/// </para>
/// </summary>
internal sealed class MediatorScopeRelease(IServiceProvider serviceProvider) : IDisposable
{
    public void Dispose() => DispatchCacheReleaser.ReleaseFor(serviceProvider);
}

