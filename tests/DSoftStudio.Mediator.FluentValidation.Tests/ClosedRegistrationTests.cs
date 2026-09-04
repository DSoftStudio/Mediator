// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation.Tests.Fixtures;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

/// <summary>
/// The open-generic registration puts <see cref="ValidationBehavior{TRequest,TResponse}"/> into the
/// chain of EVERY request, including the ones that have no validator at all — they get a pipeline
/// chain built for them and lose the direct handler path. The closed overload registers the behavior
/// for one pair only.
/// <para>
/// These assert on the descriptors <c>PrecompilePipelines()</c> leaves in the service collection
/// rather than on <c>RequestDispatch</c>'s flags, which are process-global: a collection is private to
/// its test, so nothing here depends on what another test registered first.
/// </para>
/// </summary>
public class ClosedRegistrationTests
{
    private static ServiceCollection Wired()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddScoped<IValidator<CreateUser>, CreateUserValidator>();
        return services;
    }

    [Fact]
    public void The_closed_overload_registers_the_behavior_for_that_pair_only()
    {
        var services = Wired();
        services.AddMediatorFluentValidation<CreateUser, Guid>();

        var closed = services.Where(d => d.ServiceType == typeof(IPipelineBehavior<CreateUser, Guid>)).ToList();

        closed.Count.ShouldBe(1);
        closed[0].ImplementationType.ShouldBe(typeof(ValidationBehavior<CreateUser, Guid>));

        // Scoped, matching the open registration: FluentValidation's default validator lifetime is
        // Scoped, and one Transient component would demote the whole chain.
        closed[0].Lifetime.ShouldBe(ServiceLifetime.Scoped);

        services.ShouldNotContain(d => d.ServiceType == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void A_request_outside_the_registration_gets_no_pipeline_chain_at_all()
    {
        var services = Wired();
        services.AddMediatorFluentValidation<CreateUser, Guid>();
        services.PrecompilePipelines();

        services.ShouldContain(d => d.ServiceType == typeof(PipelineChainHandler<CreateUser, Guid>));

        // Ping has no validator and never will. Under the open registration it still carries a chain,
        // built solely to run a behavior whose first line is "no validators, pass through".
        services.ShouldNotContain(d => d.ServiceType == typeof(PipelineChainHandler<Ping, int>));
    }

    [Fact]
    public void The_open_registration_by_contrast_builds_a_chain_for_every_pair()
    {
        var services = Wired();
        services.AddMediatorFluentValidation();
        services.PrecompilePipelines();

        services.ShouldContain(d => d.ServiceType == typeof(PipelineChainHandler<Ping, int>));
    }

    [Fact]
    public async Task The_registered_pair_is_still_validated()
    {
        var services = Wired();
        services.AddMediatorFluentValidation<CreateUser, Guid>();
        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Narrowing the registration must not weaken it for the pair it covers.
        await Should.ThrowAsync<MediatorValidationException>(
            () => mediator.Send(new CreateUser("", "not-an-email"), TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task A_validator_whose_pair_was_not_registered_never_runs()
    {
        var counter = new ValidatorCallCounter();

        var services = Wired();
        services.AddSingleton(counter);
        services.AddScoped<IValidator<Ping>, PingCountingValidator>();

        // Ping HAS a validator, but its pair was not registered — only CreateUser's was.
        services.AddMediatorFluentValidation<CreateUser, Guid>();
        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();

        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IMediator>()
            .Send(new Ping(21), TestContext.Current.CancellationToken);

        // This is the trade the closed overload makes, and it is silent: registering a validator is no
        // longer enough on its own. It is why the open form remains the right default when validators
        // are discovered by scanning.
        counter.Count.ShouldBe(0);
    }

    [Fact]
    public void Calling_the_closed_overload_twice_registers_one_descriptor()
    {
        var services = Wired();
        services.AddMediatorFluentValidation<CreateUser, Guid>();
        services.AddMediatorFluentValidation<CreateUser, Guid>();

        services.Count(d => d.ServiceType == typeof(IPipelineBehavior<CreateUser, Guid>)).ShouldBe(1);
    }

    [Fact]
    public void The_closed_overload_stands_down_when_the_open_one_is_already_registered()
    {
        var services = Wired();
        services.AddMediatorFluentValidation();
        services.AddMediatorFluentValidation<CreateUser, Guid>();

        // The open registration already covers this pair; a closed descriptor as well would run every
        // validator for it twice.
        services.ShouldNotContain(d => d.ServiceType == typeof(IPipelineBehavior<CreateUser, Guid>));
    }
}
