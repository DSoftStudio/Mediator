// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.Tests.Infrastructure;

// ── Request handlers ──────────────────────────────────────────────

public sealed class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken cancellationToken)
        => new(42);
}

public sealed class VoidHandler : IRequestHandler<PingVoid, Unit>
{
    public ValueTask<Unit> Handle(PingVoid request, CancellationToken cancellationToken)
        => new(Unit.Value);
}

public sealed class LegacyPingHandler : IRequestHandler<LegacyPing, int>
{
    public ValueTask<int> Handle(LegacyPing request, CancellationToken cancellationToken)
        => new(42);
}

public sealed class LegacyVoidHandler : IRequestHandler<LegacyPingVoid, Unit>
{
    public ValueTask<Unit> Handle(LegacyPingVoid request, CancellationToken cancellationToken)
        => new(Unit.Value);
}

public sealed class SlowPingHandler : IRequestHandler<SlowPing, int>
{
    public async ValueTask<int> Handle(SlowPing request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return 99;
    }
}

// ── Notification handlers ─────────────────────────────────────────

public sealed class PingNotificationHandler : INotificationHandler<PingNotification>
{
    public int CallCount { get; private set; }

    public Task Handle(PingNotification notification, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.CompletedTask;
    }
}

public sealed class OrderedNotificationHandlerA : INotificationHandler<OrderedNotification>
{
    private readonly List<string> _log;

    public OrderedNotificationHandlerA(List<string> log) => _log = log;

    public Task Handle(OrderedNotification notification, CancellationToken cancellationToken)
    {
        _log.Add("A");
        return Task.CompletedTask;
    }
}

public sealed class OrderedNotificationHandlerB : INotificationHandler<OrderedNotification>
{
    private readonly List<string> _log;

    public OrderedNotificationHandlerB(List<string> log) => _log = log;

    public Task Handle(OrderedNotification notification, CancellationToken cancellationToken)
    {
        _log.Add("B");
        return Task.CompletedTask;
    }
}

// ── Stream handlers ───────────────────────────────────────────────

public sealed class PingStreamHandler : IStreamRequestHandler<PingStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        PingStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}

public sealed class BehaviorPingStreamHandler : IStreamRequestHandler<BehaviorPingStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        BehaviorPingStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
