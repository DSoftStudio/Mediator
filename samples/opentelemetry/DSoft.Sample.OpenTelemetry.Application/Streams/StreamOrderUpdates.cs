// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.OpenTelemetry.Application.Streams;

public record StreamOrderUpdates(int Count = 3) : IStreamRequest<string>;

public sealed class StreamOrderUpdatesHandler : IStreamRequestHandler<StreamOrderUpdates, string>
{
    public async IAsyncEnumerable<string> Handle(
        StreamOrderUpdates request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            await Task.Delay(20, cancellationToken);
            yield return $"Order update #{i}";
        }
    }
}
