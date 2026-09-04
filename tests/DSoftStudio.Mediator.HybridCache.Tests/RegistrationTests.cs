// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Cache = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace DSoftStudio.Mediator.HybridCache.Tests;

public class RegistrationTests
{
    [Fact]
    public void AddMediatorCaching_registers_behavior()
    {
        var services = new ServiceCollection();
        services.AddMediatorHybridCache();

        var descriptor = services.Single(d =>
            d.ServiceType == typeof(IPipelineBehavior<,>) &&
            d.ImplementationType == typeof(CachingBehavior<,>));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
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

    /// <summary>
    /// The premise the Singleton lifetime rests on: the behavior may only outlive a scope because
    /// its one dependency does too. If a future HybridCache release demoted this to Scoped, the
    /// behavior would be capturing a scoped service for the life of the application.
    /// </summary>
    [Fact]
    public void HybridCache_itself_is_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();

        var descriptor = services.Last(d => d.ServiceType == typeof(Cache));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// A transient pipeline component makes the generated RegisterPipeline register the chain as
    /// Transient and skip MarkPipelineChainCacheable, so every dispatch re-resolves and re-links
    /// the chain. This pins the Singleton registration that keeps it cached.
    /// </summary>
    [Fact]
    public void Caching_behavior_leaves_the_pipeline_chain_cacheable()
    {
        var services = BuildFullCollection();

        var chain = services.Last(d =>
            d.ServiceType == typeof(PipelineChainHandler<GetProduct, ProductDto>));

        RequestDispatch<GetProduct, ProductDto>.IsPipelineChainCacheable.ShouldBeTrue();
        chain.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Registering the behavior twice would nest it inside itself: the outer GetOrCreateAsync
    /// factory re-enters the inner one on the SAME key while the outer call is still in flight.
    /// </summary>
    [Fact]
    public void AddMediatorCaching_called_twice_registers_one_behavior()
    {
        var services = new ServiceCollection();

        services.AddMediatorHybridCache();
        services.AddMediatorHybridCache();

        services.Count(d =>
            d.ServiceType == typeof(IPipelineBehavior<,>) &&
            d.ImplementationType == typeof(CachingBehavior<,>))
            .ShouldBe(1);
    }

    /// <summary>
    /// The timeout is load-bearing: without the dedup guard the nested behaviors DEADLOCK rather
    /// than merely double-cache — the outer factory awaits the inner call, which joins the outer's
    /// still-in-flight stampede entry for the same key. The timeout turns that into a failure
    /// instead of a hung test run.
    /// </summary>
    [Fact(Timeout = 15_000)]
    public async Task Double_registration_still_caches_exactly_once()
    {
        var handler = new GetProductHandler();
        var provider = TestServiceProvider.Build(
            services => services.AddSingleton<IRequestHandler<GetProduct, ProductDto>>(handler),
            configureTwice: true);

        var mediator = provider.GetRequiredService<IMediator>();
        var id = Guid.NewGuid();

        await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);
        await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);

        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Behavior_rejects_a_null_cache()
    {
        Should.Throw<ArgumentNullException>(() => new CachingBehavior<Ping, int>(null!));
    }

    private static IServiceCollection BuildFullCollection()
    {
        var services = new ServiceCollection();

        services
            .AddMediator()
            .RegisterMediatorHandlers();

        services.AddHybridCache();

        services
            .AddMediatorHybridCache()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        return services;
    }
}
