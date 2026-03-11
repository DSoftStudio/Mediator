// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Benchmarks;

public sealed class PingDispatchRHandler : global::DispatchR.Abstractions.Send.IRequestHandler<PingDispatchR, ValueTask<int>>
{
    public ValueTask<int> Handle(PingDispatchR request, CancellationToken cancellationToken)
    {
        return new ValueTask<int>(42);
    }
}

public sealed class PingNotificationDispatchRHandler : global::DispatchR.Abstractions.Notification.INotificationHandler<PingNotificationDispatchR>
{
    public ValueTask Handle(PingNotificationDispatchR notification, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class PingStreamDispatchRHandler : global::DispatchR.Abstractions.Stream.IStreamRequestHandler<PingStreamDispatchR, int>
{
    public async IAsyncEnumerable<int> Handle(
        PingStreamDispatchR request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 42;
    }
}
