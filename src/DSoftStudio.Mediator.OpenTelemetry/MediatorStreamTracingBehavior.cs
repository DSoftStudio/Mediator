// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Stream pipeline behavior that creates distributed tracing spans for streamed requests.
/// The span covers the entire enumeration lifetime.
/// </summary>
public sealed class MediatorStreamTracingBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options) : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;

    public IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!options.EnableTracing || !Source.HasListeners())
            return next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return next.Handle(request, cancellationToken);

        return Instrumented(request, next, cancellationToken);
    }

    private async IAsyncEnumerable<TResponse> Instrumented(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity(
            MediatorStreamMetadata<TRequest, TResponse>.SpanName,
            ActivityKind.Internal);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("mediator.request.type", MediatorStreamMetadata<TRequest, TResponse>.RequestType);
            activity.SetTag("mediator.response.type", MediatorStreamMetadata<TRequest, TResponse>.ResponseType);
            activity.SetTag("mediator.request.kind", MediatorStreamMetadata<TRequest, TResponse>.RequestKind);
            // ADR-0049 — the concrete stream handler behind this request (resolved through the chain, never
            // instantiated), so an imported trace maps the stream span to its handler source. The request path
            // does the same in MediatorDispatchTracingObserver (streams have no dispatch port, so this stays a behavior).
            activity.SetTag("mediator.handler.type", ResolveHandlerType(next).FullName);

            options.EnrichActivity?.Invoke(activity, request);
        }

        bool success = false;
        // Per-item production metrics — measured here (the span already wraps the full enumeration) so an imported
        // trace can populate the profiler's STREAM TELEMETRY *production* block (items / TTFI / throughput), not
        // just lifecycle + duration. Stopwatch.GetTimestamp() math keeps this allocation-free and TFM-agnostic.
        long itemCount = 0;
        long startTimestamp = Stopwatch.GetTimestamp();
        long firstItemTimestamp = 0;
        try
        {
            await foreach (var item in next.Handle(request, cancellationToken).WithCancellation(cancellationToken))
            {
                if (itemCount == 0)
                    firstItemTimestamp = Stopwatch.GetTimestamp();
                itemCount++;
                yield return item;
            }
            success = true;
        }
        finally
        {
            if (activity is { IsAllDataRequested: true })
            {
                double freq            = Stopwatch.Frequency;
                double elapsedMs       = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / freq;
                double firstItemMs     = firstItemTimestamp > 0 ? (firstItemTimestamp - startTimestamp) * 1000.0 / freq : 0.0;
                double throughputPerSec = elapsedMs > 0 ? itemCount * 1000.0 / elapsedMs : 0.0;
                activity.SetTag("mediator.stream.item_count", itemCount);
                activity.SetTag("mediator.stream.first_item_ms", firstItemMs);
                activity.SetTag("mediator.stream.throughput_per_sec", throughputPerSec);
            }
            activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
    }

    /// <summary>
    /// The concrete stream handler type at the end of the chain — via <see cref="IPipelineHandlerTypeAccessor"/>
    /// when <paramref name="next"/> is a chain adapter, or its runtime type when this behavior is the innermost link.
    /// </summary>
    private static Type ResolveHandlerType(IStreamRequestHandler<TRequest, TResponse> next)
        => next is IPipelineHandlerTypeAccessor accessor ? accessor.HandlerType : next.GetType();
}
