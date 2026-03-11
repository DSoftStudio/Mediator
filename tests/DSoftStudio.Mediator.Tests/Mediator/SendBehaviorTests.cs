// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Mediator;

public class SendBehaviorTests : IDisposable
{
    private readonly List<string> _log = [];
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public SendBehaviorTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();
        services.AddSingleton(_log);

        // Register two named tracking behaviors
        services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
            new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), "B1"));
        services.AddTransient<IPipelineBehavior<Ping, int>>(sp =>
            new TrackingBehavior<Ping, int>(sp.GetRequiredService<List<string>>(), "B2"));

        services.PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_WithBehaviors_ExecutesBehaviors()
    {
        await _mediator.Send(new Ping());

        _log.ShouldContain("B1:before");
        _log.ShouldContain("B2:before");
    }

    [Fact]
    public async Task Send_WithBehaviors_PreservesOrder()
    {
        await _mediator.Send(new Ping());

        // B1 registered first ? outermost ? enters first, exits last
        _log.ShouldBe(new[] {"B1:before", "B2:before", "B2:after", "B1:after"});
    }

    [Fact]
    public async Task Send_WithBehaviors_StillReturnsHandlerResult()
    {
        var result = await _mediator.Send(new Ping());

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Send_WithPassThroughBehavior_ReturnsCorrectValue()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();
        services.AddTransient<IPipelineBehavior<Ping, int>, PassThroughBehavior<Ping, int>>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new Ping());
        result.ShouldBe(42);
    }
}
