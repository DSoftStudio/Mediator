// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

// ── Commands ──────────────────────────────────────────────────────────

public record TestCommand(string Value) : ICommand<string>;

public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
{
    public ValueTask<string> Handle(TestCommand request, CancellationToken cancellationToken)
        => new($"handled:{request.Value}");
}

// ── Queries ───────────────────────────────────────────────────────────

public record TestQuery(int Id) : IQuery<string>;

public sealed class TestQueryHandler : IRequestHandler<TestQuery, string>
{
    public ValueTask<string> Handle(TestQuery request, CancellationToken cancellationToken)
        => new($"result:{request.Id}");
}

// ── Generic requests ──────────────────────────────────────────────────

public record TestRequest(string Data) : IRequest<int>;

public sealed class TestRequestHandler : IRequestHandler<TestRequest, int>
{
    public ValueTask<int> Handle(TestRequest request, CancellationToken cancellationToken)
        => new(request.Data.Length);
}

// ── Failing handler ───────────────────────────────────────────────────

public record FailingCommand(string Message) : ICommand<string>;

public sealed class FailingCommandHandler : IRequestHandler<FailingCommand, string>
{
    public ValueTask<string> Handle(FailingCommand request, CancellationToken cancellationToken)
        => throw new InvalidOperationException(request.Message);
}

// ── Notifications ─────────────────────────────────────────────────────

public record TestNotification(string Value) : INotification;

public sealed class TestNotificationHandler1 : INotificationHandler<TestNotification>
{
    public List<string> Received { get; } = [];

    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        Received.Add(notification.Value);
        return Task.CompletedTask;
    }
}

public sealed class TestNotificationHandler2 : INotificationHandler<TestNotification>
{
    public List<string> Received { get; } = [];

    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        Received.Add(notification.Value);
        return Task.CompletedTask;
    }
}

public sealed class FailingNotificationHandler : INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("notification-failed");
}

// ── Streams ───────────────────────────────────────────────────────────

public record TestStreamRequest(int Count) : IStreamRequest<int>;

public sealed class TestStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        TestStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            yield return i;
            await Task.Yield();
        }
    }
}

// ── Failing stream ────────────────────────────────────────────────────

public record FailingStreamRequest() : IStreamRequest<int>;

public sealed class FailingStreamHandler : IStreamRequestHandler<FailingStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        FailingStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 1;
        await Task.Yield();
        throw new InvalidOperationException("stream-failed");
    }
}

// ── Health check (for filter tests) ───────────────────────────────────

public record HealthCheckQuery() : IQuery<string>;

public sealed class HealthCheckHandler : IRequestHandler<HealthCheckQuery, string>
{
    public ValueTask<string> Handle(HealthCheckQuery request, CancellationToken cancellationToken)
        => new("ok");
}

public record HealthCheckStreamRequest() : IStreamRequest<int>;

public sealed class HealthCheckStreamHandler : IStreamRequestHandler<HealthCheckStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        HealthCheckStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 1;
        await Task.Yield();
    }
}

public record HealthCheckNotification() : INotification;

public sealed class HealthCheckNotificationHandler : INotificationHandler<HealthCheckNotification>
{
    public Task Handle(HealthCheckNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
