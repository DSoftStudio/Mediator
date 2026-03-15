// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Mediator;

/// <summary>
/// Tests the runtime object-based Send(object) path which uses
/// the AOT-safe <see cref="RequestObjectDispatch"/> table
/// populated at startup by the generated MediatorRegistry.
/// </summary>
public class SendObjectTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public SendObjectTests()
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
    public async Task SendObject_ReturnsCorrectValue()
    {
        object request = new Ping();

        var result = await ((ISender)_mediator).Send(request);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task SendObject_CalledTwice_ReturnsSameValue()
    {
        object request = new Ping();

        var result1 = await ((ISender)_mediator).Send(request);
        var result2 = await ((ISender)_mediator).Send(request);

        result1.ShouldBe(42);
        result2.ShouldBe(42);
    }

    [Fact]
    public async Task SendObject_WithUnitResponse_ReturnsUnit()
    {
        object request = new PingVoid();

        var result = await ((ISender)_mediator).Send(request);

        result.ShouldBe(Unit.Value);
    }

    [Fact]
    public async Task SendObject_NullRequest_ThrowsArgumentNullException()
    {
        Func<Task> act = () => ((ISender)_mediator).Send((object)null!).AsTask();

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task SendObject_UnregisteredType_ThrowsInvalidOperationException()
    {
        object request = new UnregisteredRequest();

        Func<Task> act = () => ((ISender)_mediator).Send(request).AsTask();

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task SendObject_TypedExtension_StillWorks()
    {
        // Verify the typed extension method (Ping-specific) still takes priority
        // over the Send(object) extension when the compile-time type is known.
        var result = await _mediator.Send(new Ping());

        result.ShouldBe(42);
    }

    [Fact]
    public async Task SendObject_ConcurrentCalls_AllReturnCorrectResult()
    {
        const int concurrency = 100;
        var tasks = new Task<object?>[concurrency];

        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var scope = _provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                object request = new Ping();
                return await ((ISender)mediator).Send(request);
            });
        }

        var results = await Task.WhenAll(tasks);

        results.ShouldAllBe(r => (int)r! == 42);
    }
}

/// <summary>
/// Request type with no registered handler — used to test error path.
/// </summary>
file record UnregisteredRequest : IRequest<int>;
