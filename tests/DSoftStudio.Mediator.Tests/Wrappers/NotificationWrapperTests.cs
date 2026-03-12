// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Wrappers;

/// <summary>
/// Tests the runtime object-based Publish(object) path which uses
/// the AOT-safe <see cref="NotificationObjectDispatch"/> table
/// populated at startup by the generated NotificationRegistry.
/// </summary>
public class NotificationWrapperTests : IDisposable
{
    private readonly PingNotificationHandler _handler;
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NotificationWrapperTests()
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
    public async Task PublishObject_InvokesHandler()
    {
        object notification = new PingNotification();

        await _mediator.Publish(notification);

        _handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task PublishObject_CalledTwice_UsesWrapperCache()
    {
        object notification = new PingNotification();

        await _mediator.Publish(notification);
        await _mediator.Publish(notification);

        _handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task PublishObject_NotINotification_ThrowsArgumentException()
    {
        Func<Task> act = () => _mediator.Publish("not a notification");

        var ex = await Should.ThrowAsync<ArgumentException>(act);
        ex.ParamName.ShouldBe("notification");
    }

    [Fact]
    public async Task PublishObject_NullNotification_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _mediator.Publish((object)null!);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }
}
