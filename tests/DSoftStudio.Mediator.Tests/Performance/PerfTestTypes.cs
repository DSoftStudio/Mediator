// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.Tests.Performance;

// ── Dedicated types for performance tests ──
// Each type is unique to avoid static dispatch contamination from other tests.

public record PerfPing : IRequest<int>;
public record PerfNotification : INotification;
public record PerfStream : IStreamRequest<int>;

public sealed class PerfPingHandler : IRequestHandler<PerfPing, int>
{
    public ValueTask<int> Handle(PerfPing request, CancellationToken ct) => new(42);
}

public sealed class PerfNotificationHandler : INotificationHandler<PerfNotification>
{
    public Task Handle(PerfNotification notification, CancellationToken ct) => Task.CompletedTask;
}

public sealed class PerfStreamHandler : IStreamRequestHandler<PerfStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        PerfStream request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return 1;
        yield return 2;
    }
}

public sealed class PerfPassThroughBehavior : IPipelineBehavior<PerfPing, int>
{
    public ValueTask<int> Handle(PerfPing request, IRequestHandler<PerfPing, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}
