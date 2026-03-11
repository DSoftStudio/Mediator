using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Streaming.Application.Streams;

/// <summary>
// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
/// Request that streams a countdown from <see cref="From"/> to 1.
/// Each item is yielded with a simulated delay.
/// </summary>
public record CountdownStream(int From) : IStreamRequest<int>;

public sealed class CountdownStreamHandler : IStreamRequestHandler<CountdownStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CountdownStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = request.From; i >= 1; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate async work (e.g., reading from a database cursor)
            await Task.Delay(500, cancellationToken);

            yield return i;
        }
    }
}
