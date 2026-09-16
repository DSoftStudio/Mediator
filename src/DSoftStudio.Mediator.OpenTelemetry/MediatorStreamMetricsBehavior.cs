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
public sealed class MediatorStreamMetricsBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options, MediatorMetrics metrics) : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{

    public IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!options.EnableMetrics || !metrics.RequestDuration.Enabled)
            return next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return next.Handle(request, cancellationToken);

        return Instrumented(request, next, cancellationToken);
    }

    // Same hand-driven enumerator as the tracing behavior, for the same reason: C# forbids a catch
    // clause in an iterator containing `yield return`, so the try/finally this used to have could
    // never see the exception. mediator.request.errors was therefore never incremented for a stream,
    // while its request-side twin has always recorded it — an operator watching error rate saw a clean
    // line while every stream in the application failed, and the metric contradicted the traces.
    private async IAsyncEnumerable<TResponse> Instrumented(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tags = new TagList
        {
            { "mediator.request.type", MediatorStreamMetadata<TRequest, TResponse>.RequestType },
            { "mediator.request.kind", MediatorStreamMetadata<TRequest, TResponse>.RequestKind }
        };

        metrics.RequestActive.Add(1, tags);
        var startTimestamp = Stopwatch.GetTimestamp();

        var enumerator = next.Handle(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Cancellation counts here, unlike on the span. A counter is read as a rate and
                    // split by error.type, so an OperationCanceledException shows up as its own series
                    // rather than muddying the fault line — dropping it would hide the deadline
                    // problems that are the usual reason a stream stops early.
                    var errorTags = tags;
                    errorTags.Add("error.type", ex.GetType().FullName!);
                    metrics.RequestErrors.Add(1, errorTags);
                    throw;
                }

                if (!moved)
                    break;

                // Outside the try, which is what makes the catch above legal.
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);

            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RequestDuration.Record(elapsed.TotalSeconds, tags);
            metrics.RequestActive.Add(-1, tags);
        }
    }
}