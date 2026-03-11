// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.EdgeCases;

public class NullArgumentTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NullArgumentTests()
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

    // -- Send fast path --------------------------------------------

    [Fact]
    public async Task Send_NullRequest_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _mediator.Send<Ping, int>(null!);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    // -- Publish generic -------------------------------------------

    [Fact]
    public async Task PublishGeneric_NullNotification_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _mediator.Publish<PingNotification>(null!);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    // -- Publish object --------------------------------------------

    [Fact]
    public async Task PublishObject_NullNotification_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _mediator.Publish((object)null!);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    // -- CreateStream ----------------------------------------------

    [Fact]
    public void CreateStream_NullRequest_ThrowsArgumentNullException()
    {
        Action act = () => _mediator.CreateStream<PingStream, int>(null!);

        Should.Throw<ArgumentNullException>(act);
    }
}
