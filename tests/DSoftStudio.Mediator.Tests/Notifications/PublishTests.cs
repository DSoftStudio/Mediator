// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

public class PublishTests : IDisposable
{
    private readonly PingNotificationHandler _handler;
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public PublishTests()
    {
        _handler = new PingNotificationHandler();

        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();
        services.AddSingleton<INotificationHandler<PingNotification>>(_handler);
        services.AddSingleton(_handler);

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Publish_InvokesHandler()
    {
        await _mediator.Publish(new PingNotification());

        _handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_CalledTwice_IncrementsTwice()
    {
        await _mediator.Publish(new PingNotification());
        await _mediator.Publish(new PingNotification());

        _handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Publish_NoHandlersRegistered_CompletesWithoutError()
    {
        // UnregisteredNotification has no dispatch table entry — Handlers is null by default.
        var task = _mediator.Publish(new UnregisteredNotification());
        await task;

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Publish_EmptyHandlersArray_CompletesWithoutError()
    {
        // Initialize with an empty array — first write wins (write-once).
        NotificationDispatch<UnregisteredNotification>.TryInitialize([]);

        var task = _mediator.Publish(new UnregisteredNotification());
        await task;

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
