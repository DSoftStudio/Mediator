// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using Mediator;

namespace MediatorBenchmarks;

// ── Messages ──────────────────────────────────────────────
public sealed record PingMartin : IRequest<int>;
public sealed record PingNotificationMartin : INotification;
public sealed record PingStreamMartin : IStreamRequest<int>;

// ── Handlers ──────────────────────────────────────────────
public sealed class PingMartinHandler : IRequestHandler<PingMartin, int>
{
    public ValueTask<int> Handle(PingMartin request, CancellationToken cancellationToken)
    {
        return new ValueTask<int>(42);
    }
}

public sealed class PingNotificationMartinHandler : INotificationHandler<PingNotificationMartin>
{
    public ValueTask Handle(PingNotificationMartin notification, CancellationToken cancellationToken)
    {
        return default;
    }
}

public sealed class PingStreamMartinHandler : IStreamRequestHandler<PingStreamMartin, int>
{
    public async IAsyncEnumerable<int> Handle(
        PingStreamMartin request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 42;
    }
}
