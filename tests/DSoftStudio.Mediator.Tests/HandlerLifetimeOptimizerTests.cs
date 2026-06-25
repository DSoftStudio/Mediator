// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using DSoftStudio.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DSoftStudio.Mediator.Tests;

/// <summary>
/// Unit tests for <see cref="HandlerLifetimeOptimizer"/> - the two-phase (Stage at registration, Apply at
/// finalization) pass that raises an auto-detected handler lifetime from the conservative Transient default
/// to the longest SAFE lifetime its dependencies allow. Driven with a synthetic descriptor (no real handler
/// types) so it is fully isolated, and exercises the order-independence and the reference-identity override
/// guard directly.
/// </summary>
public class HandlerLifetimeOptimizerTests
{
    private interface IProbe { }
    private sealed class ProbeImpl : IProbe { }
    private sealed class SingletonDep { }
    private sealed class ScopedDep { }
    private sealed class TransientDep { }

    // Adds the probe handler the way the generator does (explicit Transient descriptor) and stages it.
    private static ServiceDescriptor StageProbe(IServiceCollection services, params Type[] deps)
    {
        var descriptor = ServiceDescriptor.Transient(typeof(IProbe), typeof(ProbeImpl));
        services.Add(descriptor);
        HandlerLifetimeOptimizer.Stage(services, new[] { (descriptor, deps) });
        return descriptor;
    }

    private static ServiceLifetime LifetimeOf(IServiceCollection services) =>
        services.Last(d => d.ServiceType == typeof(IProbe)).Lifetime;

    [Fact]
    public void AllSingletonDependencies_UpgradesHandlerToSingleton()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        StageProbe(s, typeof(SingletonDep));

        HandlerLifetimeOptimizer.Apply(s);

        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(s));
    }

    [Fact]
    public void DependencyRegisteredAfterStaging_IsStillSeenAtApply()
    {
        var s = new ServiceCollection();
        StageProbe(s, typeof(SingletonDep));   // handler staged BEFORE its dependency exists
        s.AddSingleton<SingletonDep>();         // dependency registered AFTER - the common composition order

        HandlerLifetimeOptimizer.Apply(s);

        // Order-independence: Apply sees the dependency because it runs at finalization, not at staging.
        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(s));
    }

    [Fact]
    public void AnyScopedDependency_CapsHandlerAtScoped()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        s.AddScoped<ScopedDep>();
        StageProbe(s, typeof(SingletonDep), typeof(ScopedDep));

        HandlerLifetimeOptimizer.Apply(s);

        Assert.Equal(ServiceLifetime.Scoped, LifetimeOf(s));
    }

    [Fact]
    public void AnyTransientDependency_KeepsHandlerTransient()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        s.AddTransient<TransientDep>();
        StageProbe(s, typeof(SingletonDep), typeof(TransientDep));

        HandlerLifetimeOptimizer.Apply(s);

        Assert.Equal(ServiceLifetime.Transient, LifetimeOf(s));
    }

    [Fact]
    public void UnregisteredDependency_KeepsHandlerTransient()
    {
        var s = new ServiceCollection();
        StageProbe(s, typeof(SingletonDep)); // SingletonDep deliberately never registered

        HandlerLifetimeOptimizer.Apply(s);

        Assert.Equal(ServiceLifetime.Transient, LifetimeOf(s));
    }

    [Fact]
    public void UserReRegistrationWithDifferentLifetime_IsRespected()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        StageProbe(s, typeof(SingletonDep));
        s.AddScoped<IProbe, ProbeImpl>(); // user override AFTER staging - must be left alone

        HandlerLifetimeOptimizer.Apply(s);

        Assert.Equal(ServiceLifetime.Scoped, LifetimeOf(s)); // respected; NOT raised to Singleton
    }

    [Fact]
    public void IdenticalUserReRegistration_ForcesTransient_ReferenceGuard()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        StageProbe(s, typeof(SingletonDep));
        s.AddTransient<IProbe, ProbeImpl>(); // user re-adds an IDENTICAL Transient to force Transient

        HandlerLifetimeOptimizer.Apply(s);

        // The reference-identity guard distinguishes the user's new descriptor from the generator's, so the
        // explicit Transient is preserved - a heuristic that matched on (impl, Transient) would wrongly upgrade.
        Assert.Equal(ServiceLifetime.Transient, LifetimeOf(s));
    }

    [Fact]
    public void ApplyWithoutStaging_IsNoOp()
    {
        var s = new ServiceCollection();
        s.AddTransient<IProbe, ProbeImpl>(); // not staged

        HandlerLifetimeOptimizer.Apply(s); // must not throw or change anything

        Assert.Equal(ServiceLifetime.Transient, LifetimeOf(s));
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        StageProbe(s, typeof(SingletonDep));

        HandlerLifetimeOptimizer.Apply(s);
        HandlerLifetimeOptimizer.Apply(s); // second run finds nothing staged

        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(s));
    }

    [Fact]
    public void Upgraded_SingletonHandlerWithSingletonDep_BuildsAndValidates()
    {
        var s = new ServiceCollection();
        s.AddSingleton<SingletonDep>();
        StageProbe(s, typeof(SingletonDep));
        HandlerLifetimeOptimizer.Apply(s);

        // Singleton consuming a Singleton is captive-free: BuildServiceProvider with scope validation succeeds.
        using var provider = s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.Same(provider.GetRequiredService<IProbe>(), provider.GetRequiredService<IProbe>());
    }
}
