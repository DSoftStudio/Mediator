// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

public class HandlerSpoofingTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public HandlerSpoofingTests()
    {
        var services = new ServiceCollection();
        services.AddMediator();

        // No handler or chain registered for FakePing.

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_ShouldThrow_WhenHandlerNotRegistered()
    {
        var request = new FakePing();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _mediator.Send<FakePing, int>(request));
    }

    }
