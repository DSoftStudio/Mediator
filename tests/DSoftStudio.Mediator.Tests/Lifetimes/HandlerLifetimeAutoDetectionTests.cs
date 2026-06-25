// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Lifetimes;

// -- Dependencies with known lifetimes (registered BEFORE RegisterMediatorHandlers so the
//    generated HandlerLifetimeOptimizer pass can see them) ---

public sealed class AutoSingletonDep { }
public sealed class AutoScopedDep { }

// -- Requests + handlers exercising the auto-detection path end-to-end ---

public sealed record AutoSingletonReq : IRequest<int>;
public sealed class AutoSingletonReqHandler(AutoSingletonDep dep) : IRequestHandler<AutoSingletonReq, int>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    private readonly AutoSingletonDep _dep = dep;
    public ValueTask<int> Handle(AutoSingletonReq request, CancellationToken ct) => new(_dep is null ? -1 : 42);
}

public sealed record AutoScopedReq : IRequest<int>;
public sealed class AutoScopedReqHandler(AutoScopedDep dep) : IRequestHandler<AutoScopedReq, int>
{
    private readonly AutoScopedDep _dep = dep;
    public ValueTask<int> Handle(AutoScopedReq request, CancellationToken ct) => new(_dep is null ? -1 : 42);
}

public sealed record AutoUnknownDepReq : IRequest<int>;
public sealed class AutoUnknownDepReqHandler(AutoSingletonDep dep) : IRequestHandler<AutoUnknownDepReq, int>
{
    private readonly AutoSingletonDep _dep = dep;
    public ValueTask<int> Handle(AutoUnknownDepReq request, CancellationToken ct) => new(_dep is null ? -1 : 42);
}

public sealed record AutoPinnedTransientReq : IRequest<int>;
[HandlerLifetime(HandlerLifetime.Transient)]
public sealed class AutoPinnedTransientReqHandler(AutoSingletonDep dep) : IRequestHandler<AutoPinnedTransientReq, int>
{
    private readonly AutoSingletonDep _dep = dep;
    public ValueTask<int> Handle(AutoPinnedTransientReq request, CancellationToken ct) => new(_dep is null ? -1 : 42);
}

[HandlerLifetime(HandlerLifetime.Singleton)]
public sealed class AutoPinnedSingletonReqHandler(AutoSingletonDep dep) : IRequestHandler<AutoPinnedSingletonReq, int>
{
    private readonly AutoSingletonDep _dep = dep;
    public ValueTask<int> Handle(AutoPinnedSingletonReq request, CancellationToken ct) => new(_dep is null ? -1 : 42);
}
public sealed record AutoPinnedSingletonReq : IRequest<int>;

/// <summary>
/// End-to-end coverage of the generator-driven smart handler lifetime: the generated
/// <c>RegisterMediatorHandlers()</c> emits the <see cref="HandlerLifetimeOptimizer"/> call, which raises
/// a dependency-carrying handler from the conservative Transient default to the longest safe lifetime its
/// dependencies allow - unless pinned with <c>[HandlerLifetime]</c>. Each test registers the relevant
/// dependency BEFORE <c>RegisterMediatorHandlers()</c> (the normal composition-root order) so the pass sees it.
/// </summary>
public class HandlerLifetimeAutoDetectionTests
{
    private static ServiceLifetime HandlerLifetimeOf<TReq, TRes>(IServiceCollection services)
        where TReq : IRequest<TRes> =>
        services.Last(d => d.ServiceType == typeof(IRequestHandler<TReq, TRes>)).Lifetime;

    [Fact]
    public void SingletonDependency_AutoUpgradesHandlerToSingleton_AndReusesAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AutoSingletonDep>();                 // dep registered before the mediator
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines(); // upgrade applied at finalization

        HandlerLifetimeOf<AutoSingletonReq, int>(services)
            .ShouldBe(ServiceLifetime.Singleton, "all-singleton deps -> handler raised to Singleton");

        // Runtime proof: one shared instance across scopes (zero per-request handler allocation).
        using var provider = services.BuildServiceProvider();
        Guid id1, id2;
        using (var s1 = provider.CreateScope())
            id1 = ((AutoSingletonReqHandler)s1.ServiceProvider.GetRequiredService<IRequestHandler<AutoSingletonReq, int>>()).InstanceId;
        using (var s2 = provider.CreateScope())
            id2 = ((AutoSingletonReqHandler)s2.ServiceProvider.GetRequiredService<IRequestHandler<AutoSingletonReq, int>>()).InstanceId;

        id1.ShouldBe(id2, "auto-Singleton handler is shared across scopes");
    }

    [Fact]
    public void DependencyRegisteredAfterRegisterHandlers_ButBeforePrecompile_IsUpgraded()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();   // handler staged; dependency not yet registered
        services.AddSingleton<AutoSingletonDep>();            // realistic order: infrastructure registered AFTER
        services.PrecompilePipelines();                       // finalization sees the dependency and upgrades

        HandlerLifetimeOf<AutoSingletonReq, int>(services)
            .ShouldBe(
                ServiceLifetime.Singleton,
                "a dependency registered after RegisterMediatorHandlers but before finalization is still seen");
    }

    [Fact]
    public void ScopedDependency_AutoCapsHandlerAtScoped()
    {
        var services = new ServiceCollection();
        services.AddScoped<AutoScopedDep>();
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();

        HandlerLifetimeOf<AutoScopedReq, int>(services)
            .ShouldBe(ServiceLifetime.Scoped, "a scoped dep caps the handler at Scoped");
    }

    [Fact]
    public void UnregisteredDependency_LeavesHandlerTransient()
    {
        var services = new ServiceCollection();
        // AutoSingletonDep deliberately NEVER registered.
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();

        HandlerLifetimeOf<AutoUnknownDepReq, int>(services)
            .ShouldBe(ServiceLifetime.Transient, "an unknown dep keeps the conservative Transient default");
    }

    [Fact]
    public void PinnedTransient_StaysTransient_DespiteSingletonDependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AutoSingletonDep>();
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();

        HandlerLifetimeOf<AutoPinnedTransientReq, int>(services)
            .ShouldBe(ServiceLifetime.Transient, "[HandlerLifetime(Transient)] opts out of the upgrade");
    }

    [Fact]
    public void PinnedSingleton_ForcesSingleton_EvenWithUnregisteredDependency()
    {
        var services = new ServiceCollection();
        // AutoSingletonDep NOT registered: auto-detection would stay Transient, but the attribute forces Singleton.
        services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();

        HandlerLifetimeOf<AutoPinnedSingletonReq, int>(services)
            .ShouldBe(ServiceLifetime.Singleton, "[HandlerLifetime(Singleton)] pins Singleton regardless of deps");
    }
}
