// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Streams;

public class StreamBehaviorTests : IDisposable
{
    private readonly List<string> _log = [];
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public StreamBehaviorTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications();
        services.AddSingleton(_log);

        services.AddTransient<IStreamPipelineBehavior<BehaviorPingStream, int>>(sp =>
            new TrackingStreamBehavior<BehaviorPingStream, int>(sp.GetRequiredService<List<string>>(), "SB1"));
        services.AddTransient<IStreamPipelineBehavior<BehaviorPingStream, int>>(sp =>
            new TrackingStreamBehavior<BehaviorPingStream, int>(sp.GetRequiredService<List<string>>(), "SB2"));

        services.PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task CreateStream_WithBehaviors_BehaviorsExecute()
    {
        var values = new List<int>();
        await foreach (var v in _mediator.CreateStream(new BehaviorPingStream()))
            values.Add(v);

        _log.ShouldContain("SB1:enter");
        _log.ShouldContain("SB2:enter");
        values.ShouldBe(new[] {1, 2, 3});
    }

    [Fact]
    public async Task CreateStream_WithBehaviors_OrderPreserved()
    {
        await foreach (var _ in _mediator.CreateStream(new BehaviorPingStream())) { }

        // SB1 registered first, SB2 second.
        // Behaviors are reversed during chaining, so SB1 is outermost ? enters first.
        _log.ShouldBe(new[] {"SB1:enter", "SB2:enter"});
    }
}
