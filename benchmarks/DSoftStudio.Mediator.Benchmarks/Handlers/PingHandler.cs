// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

public sealed class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken cancellationToken)
    {
        return new ValueTask<int>(42);
    }
}

public sealed class PingWithPipelineHandler : IRequestHandler<PingWithPipeline, int>
{
    public ValueTask<int> Handle(PingWithPipeline request, CancellationToken cancellationToken)
    {
        return new ValueTask<int>(42);
    }
}

public sealed class PingNotificationHandler : INotificationHandler<PingNotification>
{
    public Task Handle(PingNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class PingStreamHandler : IStreamRequestHandler<PingStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        PingStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 42;
    }
}
