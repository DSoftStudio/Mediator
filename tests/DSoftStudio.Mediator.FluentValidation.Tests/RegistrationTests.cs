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
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
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
