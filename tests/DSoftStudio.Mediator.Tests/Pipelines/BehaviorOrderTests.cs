// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

public class BehaviorOrderTests
{
    [Fact]
    public async Task Behaviors_ExecuteInRegistrationOrder_OuterToInner()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
        services.AddSingleton(log);

        services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
            new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), "First"));
        services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
            new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), "Second"));
        services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
            new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), "Third"));

        // PipelineChainHandler must be registered for PipelineBuilder.Build to detect behaviors.
        services.AddTransient<PipelineChainHandler<Ping, int>>();

        using var sp = services.BuildServiceProvider();

        var pipeline = PipelineBuilder.Build<Ping, int>();
        await pipeline(new Ping(), sp, CancellationToken.None);

        log.ShouldBe(new[] {
            "First:before",
            "Second:before",
            "Third:before",
            "Third:after",
            "Second:after",
            "First:after" });
    }

    [Fact]
    public async Task FiveBehaviors_AllExecute()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
        services.AddSingleton(log);

        for (int i = 1; i <= 5; i++)
        {
            var name = $"B{i}";
            services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
                new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), name));
        }

        services.AddTransient<PipelineChainHandler<Ping, int>>();

        using var sp = services.BuildServiceProvider();

        var pipeline = PipelineBuilder.Build<Ping, int>();
        var result = await pipeline(new Ping(), sp, CancellationToken.None);

        result.ShouldBe(42);
        log.Where(e => e.EndsWith(":before")).Count().ShouldBe(5);
        log.Where(e => e.EndsWith(":after")).Count().ShouldBe(5);
    }
}
