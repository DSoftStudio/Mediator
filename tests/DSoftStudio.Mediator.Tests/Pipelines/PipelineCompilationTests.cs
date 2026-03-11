// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

public class PipelineCompilationTests
{
    [Fact]
    public void Build_ReturnsNonNullDelegate()
    {
        var pipeline = PipelineBuilder.Build<Ping, int>();

        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public async Task Build_DelegateResolvesHandlerAndReturnsResult()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
        using var sp = services.BuildServiceProvider();

        var pipeline = PipelineBuilder.Build<Ping, int>();

        var result = await pipeline(new Ping(), sp, CancellationToken.None);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Build_DelegateChainsBehaviors()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
        services.AddSingleton(log);
        services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
            new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), "P1"));
        services.AddTransient<PipelineChainHandler<Ping, int>>();
        using var sp = services.BuildServiceProvider();

        var pipeline = PipelineBuilder.Build<Ping, int>();
        await pipeline(new Ping(), sp, CancellationToken.None);

        log.ShouldBe(new[] {"P1:before", "P1:after"});
    }

    [Fact]
    public async Task Build_NoBehaviors_DirectlyInvokesHandler()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
        using var sp = services.BuildServiceProvider();

        var pipeline = PipelineBuilder.Build<Ping, int>();
        var result = await pipeline(new Ping(), sp, CancellationToken.None);

        result.ShouldBe(42);
    }
}
