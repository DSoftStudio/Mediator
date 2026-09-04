// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

public class RegistrationTests
{
    [Fact]
    public void AddMediatorFluentValidation_registers_validation_behavior()
    {
        var services = new ServiceCollection();
        services.AddMediatorFluentValidation();

        var descriptor = services.SingleOrDefault(s =>
            s.ServiceType == typeof(IPipelineBehavior<,>) &&
            s.ImplementationType == typeof(ValidationBehavior<,>));

        descriptor.ShouldNotBeNull();

        // Scoped keeps the generated PipelineChainHandler cacheable for EVERY request in the app;
        // a Transient behavior demotes the whole chain to a per-dispatch resolve.
        // See PipelineLifetimeTests for the end-to-end pin.
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMediatorFluentValidation_called_twice_registers_the_behavior_once()
    {
        var services = new ServiceCollection();

        // A shared library and the host application both wiring validation up.
        services.AddMediatorFluentValidation();
        services.AddMediatorFluentValidation();

        var count = services.Count(s =>
            s.ServiceType == typeof(IPipelineBehavior<,>) &&
            s.ImplementationType == typeof(ValidationBehavior<,>));

        count.ShouldBe(1);
    }

    [Fact]
    public void AddMediatorFluentValidation_throws_on_null_services()
    {
        IServiceCollection? services = null;
        Should.Throw<ArgumentNullException>(() => services!.AddMediatorFluentValidation());
    }

    [Fact]
    public void AddMediatorFluentValidation_returns_same_collection_for_chaining()
    {
        var services = new ServiceCollection();
        var result = services.AddMediatorFluentValidation();
        result.ShouldBeSameAs(services);
    }
}
