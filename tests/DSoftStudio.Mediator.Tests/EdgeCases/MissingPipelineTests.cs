// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.EdgeCases;

// Unique types whose pipelines are never precompiled.
public record NeverCompiledRequest : IRequest<int>;
public record NeverCompiledStream : IStreamRequest<int>;

public class MissingPipelineTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public MissingPipelineTests()
    {
        var services = new ServiceCollection();
        services.AddMediator();
        // No handlers or chains registered — Send must fail.
        // NeverCompiledStream has no generated handler, so StreamDispatch is null by default.
        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_MissingPipeline_ThrowsInvalidOperationException()
    {
        Func<Task> act = async () => await _mediator.Send<NeverCompiledRequest, int>(new NeverCompiledRequest());

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task CreateStream_MissingHandler_ThrowsInvalidOperationException()
    {
        Func<Task> act = async () =>
        {
            await foreach (var _ in _mediator.CreateStream<NeverCompiledStream, int>(new NeverCompiledStream()))
            {
            }
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("not registered");
    }

    [Fact]
    public async Task PublishObject_NotINotification_ThrowsArgumentException()
    {
        Func<Task> act = () => _mediator.Publish("not a notification");

        var ex = await Should.ThrowAsync<ArgumentException>(act);
        ex.Message.ShouldContain("does not implement INotification");
    }
}
