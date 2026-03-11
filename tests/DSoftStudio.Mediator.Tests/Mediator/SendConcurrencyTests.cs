// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Mediator;

public class SendConcurrencyTests
{
    [Fact]
    public async Task Send_ParallelCalls_AllReturnCorrectResult()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        using var provider = services.BuildServiceProvider();

        const int concurrency = 100;
        var tasks = new Task<int>[concurrency];

        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new Ping());
            });
        }

        var results = await Task.WhenAll(tasks);

        results.ShouldAllBe(r => r == 42);
    }
}
