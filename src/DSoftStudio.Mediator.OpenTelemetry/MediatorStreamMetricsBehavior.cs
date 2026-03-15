// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Stream pipeline behavior that records metrics for streamed requests.
/// Duration covers the entire enumeration lifetime.
/// </summary>
public sealed class MediatorStreamMetricsBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options) : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{

    public IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!options.EnableMetrics || !MediatorInstrumentation.RequestDuration.Enabled)
            return next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return next.Handle(request, cancellationToken);

        return Instrumented(request, next, cancellationToken);
    }

    private static async IAsyncEnumerable<TResponse> Instrumented(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tags = new TagList
        {
            { "mediator.request.type", MediatorStreamMetadata<TRequest, TResponse>.RequestType },
            { "mediator.request.kind", MediatorStreamMetadata<TRequest, TResponse>.RequestKind }
        };

        MediatorInstrumentation.RequestActive.Add(1, tags);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await foreach (var item in next.Handle(request, cancellationToken).WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            MediatorInstrumentation.RequestDuration.Record(elapsed.TotalSeconds, tags);
            MediatorInstrumentation.RequestActive.Add(-1, tags);
        }
    }
}
