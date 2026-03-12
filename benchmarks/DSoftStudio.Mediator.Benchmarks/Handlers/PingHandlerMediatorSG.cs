// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

namespace Benchmarks;

public sealed class PingMediatorSGHandler : global::Mediator.IRequestHandler<PingMediatorSG, int>
{
    public ValueTask<int> Handle(PingMediatorSG request, CancellationToken cancellationToken)
    {
        return new ValueTask<int>(42);
    }
}

public sealed class PingNotificationMediatorSGHandler : global::Mediator.INotificationHandler<PingNotificationMediatorSG>
{
    public ValueTask Handle(PingNotificationMediatorSG notification, CancellationToken cancellationToken)
    {
        return default;
    }
}

public sealed class PingStreamMediatorSGHandler : global::Mediator.IStreamRequestHandler<PingStreamMediatorSG, int>
{
    public async IAsyncEnumerable<int> Handle(
        PingStreamMediatorSG request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 42;
    }
}
