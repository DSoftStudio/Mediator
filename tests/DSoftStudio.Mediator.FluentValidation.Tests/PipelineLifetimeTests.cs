// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.FluentValidation.Tests.Fixtures;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

/// <summary>
/// The validation behavior is registered into every request's pipeline, so its lifetime decides the
/// lifetime the generated <c>RegisterPipeline</c> gives the <see cref="PipelineChainHandler{TRequest,TResponse}"/>:
/// a single Transient pipeline component makes the chain Transient and suppresses
/// <c>MarkPipelineChainCacheable</c>, and the chain is then re-resolved and re-linked from DI on every
/// dispatch of every request in the application. These pin the Scoped registration that keeps it cached.
/// </summary>
public class PipelineLifetimeTests
{
    [Fact]
    public void Validation_behavior_leaves_the_pipeline_chain_scoped_and_cacheable()
    {
        var services = new ServiceCollection();

        services
            .AddMediator()
            .RegisterMediatorHandlers();

        services.AddScoped<IValidator<TransferMoney>, TransferMoneyAccountValidator>();

        services
            .AddMediatorFluentValidation()
            .PrecompilePipelines();

        var chain = services.SingleOrDefault(d =>
            d.ServiceType == typeof(PipelineChainHandler<TransferMoney, string>));

        chain.ShouldNotBeNull();
        chain.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        DSoftStudio.Mediator.RequestDispatch<TransferMoney, string>
            .IsPipelineChainCacheable.ShouldBeTrue();
    }

    [Fact]
    public void Cached_chain_is_the_same_instance_within_a_scope()
    {
        var sp = TestServiceProvider.BuildWithAllValidators();

        using var scope = sp.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<PipelineChainHandler<TransferMoney, string>>();
        var second = scope.ServiceProvider.GetRequiredService<PipelineChainHandler<TransferMoney, string>>();

        // A Transient registration would hand out a freshly re-linked chain each time — the allocation
        // the cacheable path exists to avoid.
        second.ShouldBeSameAs(first);
    }
}
