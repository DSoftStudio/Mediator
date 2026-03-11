// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Mediator;

public class SendTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public SendTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    // -- Fast path: Send<TRequest, TResponse> ----------------------

    [Fact]
    public async Task Send_ReturnsCorrectValue()
    {
        var result = await _mediator.Send(new Ping());

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Send_WithUnitResponse_ReturnsUnit()
    {
        var result = await _mediator.Send(new PingVoid());

        result.ShouldBe(Unit.Value);
    }

    [Fact]
    public async Task Send_AsyncHandler_ReturnsCorrectValue()
    {
        var result = await _mediator.Send(new SlowPing());

        result.ShouldBe(99);
    }
}
