// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests;

// ── Message unique to this test ───────────────────────────────────

public record CountedNotification : INotification;

// ── Handlers ──────────────────────────────────────────────────────

public sealed class CountedNotificationHandler1 : INotificationHandler<CountedNotification>
{
    private readonly Counter _counter;

    public CountedNotificationHandler1(Counter counter) => _counter = counter;

    public Task Handle(CountedNotification notification, CancellationToken cancellationToken)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }
}

public sealed class CountedNotificationHandler2 : INotificationHandler<CountedNotification>
{
    private readonly Counter _counter;

    public CountedNotificationHandler2(Counter counter) => _counter = counter;

    public Task Handle(CountedNotification notification, CancellationToken cancellationToken)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }
}

public sealed class Counter
{
    private int _value;
    public int Value => _value;
    public void Increment() => Interlocked.Increment(ref _value);
}

// ── Test ──────────────────────────────────────────────────────────

public class NotificationTests : IDisposable
{
    private readonly Counter _counter = new();
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NotificationTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();
        services.AddSingleton(_counter);

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Publish_Should_Invoke_All_Handlers()
    {
        await _mediator.Publish(new CountedNotification());

        _counter.Value.ShouldBe(2);
    }
}
