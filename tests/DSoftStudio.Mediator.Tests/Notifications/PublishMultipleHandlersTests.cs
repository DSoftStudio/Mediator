// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

public class PublishMultipleHandlersTests : IDisposable
{
    private readonly List<string> _log = [];
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public PublishMultipleHandlersTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();
        services.AddSingleton(_log);

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Publish_MultipleHandlers_AllInvoked()
    {
        await _mediator.Publish(new OrderedNotification());

        _log.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Publish_MultipleHandlers_OrderPreserved()
    {
        await _mediator.Publish(new OrderedNotification());

        _log.ShouldBe(new[] {"A", "B"});
    }
}
