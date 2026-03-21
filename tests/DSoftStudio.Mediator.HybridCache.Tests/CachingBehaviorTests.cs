// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache.Tests;

public class CachingBehaviorTests
{
    [Fact]
    public async Task Cached_query_returns_same_result_on_second_call()
    {
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        var id = Guid.NewGuid();
        var first = await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);
        var second = await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.Id.ShouldBe(id);
        second.Id.ShouldBe(id);
        first.Name.ShouldBe(second.Name);
    }

    [Fact]
    public async Task Cached_query_handler_is_called_only_once_for_same_key()
    {
        var handler = new GetProductHandler();
        var provider = TestServiceProvider.Build(services =>
        {
            // Override handler with our tracking instance
            services.AddSingleton<IRequestHandler<GetProduct, ProductDto>>(handler);
        });

        var mediator = provider.GetRequiredService<IMediator>();
        var id = Guid.NewGuid();

        await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);
        await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);
        await mediator.Send(new GetProduct(id), TestContext.Current.CancellationToken);

        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Different_cache_keys_invoke_handler_separately()
    {
        var handler = new GetProductHandler();
        var provider = TestServiceProvider.Build(services =>
        {
            services.AddSingleton<IRequestHandler<GetProduct, ProductDto>>(handler);
        });

        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new GetProduct(Guid.NewGuid()), TestContext.Current.CancellationToken);
        await mediator.Send(new GetProduct(Guid.NewGuid()), TestContext.Current.CancellationToken);

        handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Non_cached_request_passes_through_without_caching()
    {
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        var result1 = await mediator.Send(new Ping(21), TestContext.Current.CancellationToken);
        var result2 = await mediator.Send(new Ping(21), TestContext.Current.CancellationToken);

        result1.ShouldBe(42);
        result2.ShouldBe(42);
    }

    [Fact]
    public async Task Custom_duration_is_respected()
    {
        // GetOrder has Duration = 10 minutes — verify it doesn't throw and works
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetOrder(1), TestContext.Current.CancellationToken);

        result.ShouldBe("order:1");
    }

    [Fact]
    public async Task Cached_query_with_custom_duration_handler_called_once()
    {
        var handler = new GetOrderHandler();
        var provider = TestServiceProvider.Build(services =>
        {
            services.AddSingleton<IRequestHandler<GetOrder, string>>(handler);
        });

        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new GetOrder(42), TestContext.Current.CancellationToken);
        await mediator.Send(new GetOrder(42), TestContext.Current.CancellationToken);

        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Cached_command_works()
    {
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        var first = await mediator.Send(new GetCachedCount(), TestContext.Current.CancellationToken);
        var second = await mediator.Send(new GetCachedCount(), TestContext.Current.CancellationToken);

        // Both should return the same cached value
        first.ShouldBe(second);
    }

    [Fact]
    public async Task CacheKey_includes_request_identity()
    {
        var id = Guid.NewGuid();
        var request = new GetProduct(id);

        request.CacheKey.ShouldBe($"products:{id}");
    }

    [Fact]
    public async Task Default_duration_is_60_seconds()
    {
        var request = new GetProduct(Guid.NewGuid());
        ICachedRequest cached = request;

        cached.Duration.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Custom_duration_overrides_default()
    {
        var request = new GetOrder(1);
        ICachedRequest cached = request;

        cached.Duration.ShouldBe(TimeSpan.FromMinutes(10));
    }
}
