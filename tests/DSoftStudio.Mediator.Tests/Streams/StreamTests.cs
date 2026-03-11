// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Streams;

public class StreamTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public StreamTests()
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

    [Fact]
    public async Task CreateStream_ReturnsCorrectSequence()
    {
        var values = new List<int>();

        await foreach (var value in _mediator.CreateStream(new PingStream()))
        {
            values.Add(value);
        }

        values.ShouldBe(new[] {1, 2, 3});
    }

    [Fact]
    public async Task CreateStream_CalledTwice_ProducesIndependentSequences()
    {
        var first = new List<int>();
        var second = new List<int>();

        await foreach (var v in _mediator.CreateStream(new PingStream()))
            first.Add(v);

        await foreach (var v in _mediator.CreateStream(new PingStream()))
            second.Add(v);

        first.ShouldBe(second);
    }
}
