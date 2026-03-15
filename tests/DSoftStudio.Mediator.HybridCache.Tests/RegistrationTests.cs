// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache.Tests;

public class RegistrationTests
{
    [Fact]
    public void AddMediatorCaching_registers_behavior()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        var descriptor = services.Single(d =>
            d.ServiceType == typeof(IPipelineBehavior<,>) &&
            d.ImplementationType == typeof(CachingBehavior<,>));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddMediatorCaching_throws_on_null_services()
    {
        IServiceCollection? services = null;
        Should.Throw<ArgumentNullException>(() => services!.AddMediatorHybridCache());
    }

    [Fact]
    public void Full_pipeline_builds_without_error()
    {
        var provider = TestServiceProvider.Build();
        provider.ShouldNotBeNull();
    }
}
