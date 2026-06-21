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

        // Resolve the pre-wired chain directly — PipelineChainHandler pre-links the behaviors
        // in registration order (outer → inner), which is exactly what the live Send path runs.
        services.AddTransient<PipelineChainHandler<Ping, int>>();

        using var sp = services.BuildServiceProvider();

        var chain = sp.GetRequiredService<PipelineChainHandler<Ping, int>>();
        await chain.Handle(new Ping(), TestContext.Current.CancellationToken);

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

        var chain = sp.GetRequiredService<PipelineChainHandler<Ping, int>>();
        var result = await chain.Handle(new Ping(), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        log.Where(e => e.EndsWith(":before")).Count().ShouldBe(5);
        log.Where(e => e.EndsWith(":after")).Count().ShouldBe(5);
    }
}
