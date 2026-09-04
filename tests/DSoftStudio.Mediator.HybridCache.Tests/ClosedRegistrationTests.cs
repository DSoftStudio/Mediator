// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache.Tests;

/// <summary>
/// The open-generic registration puts <see cref="CachingBehavior{TRequest,TResponse}"/> into the chain
/// of EVERY request, so a request that never caches still gets a pipeline chain built for it and loses
/// the direct handler path. The closed overload registers the behavior for one pair only.
/// <para>
/// These assert on the descriptors the generated <c>PrecompilePipelines()</c> leaves in the service
/// collection rather than on <c>RequestDispatch</c>'s flags, which are process-global: a collection is
/// private to its test, so nothing here depends on what another test registered first.
/// </para>
/// </summary>
public class ClosedRegistrationTests
{
    private static ServiceCollection Wired()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddHybridCache();
        return services;
    }

    [Fact]
    public void The_closed_overload_registers_the_behavior_for_that_pair_only()
    {
        var services = Wired();
        services.AddMediatorHybridCache<GetProduct, ProductDto>();

        var closed = services.Where(d => d.ServiceType == typeof(IPipelineBehavior<GetProduct, ProductDto>)).ToList();

        closed.Count.ShouldBe(1);
        closed[0].ImplementationType.ShouldBe(typeof(CachingBehavior<GetProduct, ProductDto>));

        // Singleton for the same reason the open registration is: one Transient component demotes the
        // whole chain and clears the cacheable flag.
        closed[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);

        // And nothing open, which is the entire point — an open descriptor reaches every pair.
        services.ShouldNotContain(d => d.ServiceType == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void A_request_that_did_not_opt_in_gets_no_pipeline_chain_at_all()
    {
        var services = Wired();
        services.AddMediatorHybridCache<GetProduct, ProductDto>();
        services.PrecompilePipelines();

        // The pair that opted in is wired up...
        services.ShouldContain(d => d.ServiceType == typeof(PipelineChainHandler<GetProduct, ProductDto>));

        // ...and Ping, which caches nothing, has no chain registered for it. That absence IS the fast
        // path: with no chain the dispatch goes straight to the handler. Under the open registration
        // this descriptor exists for every pair in the application.
        services.ShouldNotContain(d => d.ServiceType == typeof(PipelineChainHandler<Ping, int>));
    }

    [Fact]
    public void The_open_registration_by_contrast_builds_a_chain_for_every_pair()
    {
        var services = Wired();
        services.AddMediatorHybridCache();
        services.PrecompilePipelines();

        // The control for the test above: same fixtures, open registration, and Ping — which will
        // never implement ICachedRequest — now carries a chain purely because caching was switched on
        // somewhere else in the application.
        services.ShouldContain(d => d.ServiceType == typeof(PipelineChainHandler<Ping, int>));
    }

    [Fact]
    public async Task The_registered_pair_still_caches()
    {
        var handler = new GetProductHandler();

        var services = Wired();
        services.AddSingleton<IRequestHandler<GetProduct, ProductDto>>(handler);
        services.AddMediatorHybridCache<GetProduct, ProductDto>();
        services.PrecompilePipelines().PrecompileNotifications().PrecompileStreams();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var request = new GetProduct(Guid.NewGuid());

        await mediator.Send(request, TestContext.Current.CancellationToken);
        await mediator.Send(request, TestContext.Current.CancellationToken);

        // Narrowing the registration must not weaken it: the second send is served from the cache.
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Calling_the_closed_overload_twice_registers_one_descriptor()
    {
        var services = Wired();
        services.AddMediatorHybridCache<GetProduct, ProductDto>();
        services.AddMediatorHybridCache<GetProduct, ProductDto>();

        services.Count(d => d.ServiceType == typeof(IPipelineBehavior<GetProduct, ProductDto>)).ShouldBe(1);
    }

    [Fact]
    public void The_closed_overload_stands_down_when_the_open_one_is_already_registered()
    {
        var services = Wired();
        services.AddMediatorHybridCache();
        services.AddMediatorHybridCache<GetProduct, ProductDto>();

        // The open registration is a superset: PrecompilePipelines closes it over every pair, this one
        // included. A closed descriptor as well would put the behavior in this chain twice, and the
        // outer GetOrCreateAsync would re-enter the inner one on the same key.
        services.ShouldNotContain(d => d.ServiceType == typeof(IPipelineBehavior<GetProduct, ProductDto>));
    }

    [Fact]
    public void The_closed_overload_only_accepts_a_request_that_opted_into_caching()
    {
        var parameter = typeof(HybridCacheServiceCollectionExtensions)
            .GetMethods()
            .Single(m => m.Name == nameof(HybridCacheServiceCollectionExtensions.AddMediatorHybridCache)
                      && m.IsGenericMethodDefinition)
            .GetGenericArguments()[0];

        // Registering caching for a request that never implements ICachedRequest would build a chain
        // to run a behavior that always passes through. The closed form can refuse it at compile time
        // — the open one cannot — so the constraint is part of the contract, not a detail.
        parameter.GetGenericParameterConstraints().ShouldContain(typeof(ICachedRequest));
    }
}
