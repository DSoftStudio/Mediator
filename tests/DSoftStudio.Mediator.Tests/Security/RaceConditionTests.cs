// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

public class RaceConditionTests
{
    [Fact]
    public async Task Send_ConcurrentFromManyThreads_NoExceptionsAndCorrectResults()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var provider = services.BuildServiceProvider();

        var tasks = Enumerable.Range(0, 1000).Select(_ => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return await mediator.Send(new Ping());
        }));

        var results = await Task.WhenAll(tasks);

        results.Length.ShouldBe(1000);
        results.ShouldAllBe(r => r == 42);
    }

    [Fact]
    public void Send_ParallelFor_NoExceptionsAndCorrectResults()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var provider = services.BuildServiceProvider();

        var results = new int[1000];
        var exceptions = new ConcurrentBag<Exception>();

        Parallel.For(0, 1000, i =>
        {
            try
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                results[i] = mediator.Send(new Ping()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.ShouldBeEmpty();
        results.ShouldAllBe(r => r == 42);
    }
}
