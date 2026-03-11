// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Benchmarks;

public sealed class PingMediatRHandler : MediatR.IRequestHandler<PingMediatR, int>
{
    public Task<int> Handle(PingMediatR request, CancellationToken cancellationToken)
    {
        return Task.FromResult(42);
    }
}

public sealed class PingNotificationMediatRHandler : MediatR.INotificationHandler<PingNotificationMediatR>
{
    public Task Handle(PingNotificationMediatR notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class PingStreamMediatRHandler : MediatR.IStreamRequestHandler<PingStreamMediatR, int>
{
    public async IAsyncEnumerable<int> Handle(
        PingStreamMediatR request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 42;
    }
}
