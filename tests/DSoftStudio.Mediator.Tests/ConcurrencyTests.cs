// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task Send_Should_Be_ThreadSafe()
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

        results.ShouldAllBe(r => r == 42);
    }
}
