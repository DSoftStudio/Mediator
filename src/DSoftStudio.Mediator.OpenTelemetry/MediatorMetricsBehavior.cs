// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Pipeline behavior that records metrics (duration, active count, errors) for mediator requests.
/// </summary>
public sealed class MediatorMetricsBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options, MediatorMetrics metrics) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{

    public ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Both gates are read per call on purpose: a MeterProvider can be built after the container
        // is, so "metrics are off" is not a fact that can be settled once at construction.
        if (!options.EnableMetrics || !metrics.RequestDuration.Enabled)
            return next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return next.Handle(request, cancellationToken);

        return Instrumented(request, next, cancellationToken);
    }

    /// <summary>
    /// The measured path, split out so a dispatch nobody is measuring never builds an async state
    /// machine. This behavior is registered as an OPEN GENERIC, so it sits on every request in the
    /// application — the cost of the disabled path is paid by every dispatch, not just instrumented
    /// ones. Both stream behaviors in this package already split this way; the request path did not.
    /// </summary>
    private async ValueTask<TResponse> Instrumented(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var tags = new TagList
        {
            { "mediator.request.type", MediatorTelemetryMetadata<TRequest, TResponse>.RequestType },
            { "mediator.request.kind", MediatorTelemetryMetadata<TRequest, TResponse>.RequestKind }
        };

        metrics.RequestActive.Add(1, tags);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            return await next.Handle(request, cancellationToken);
        }
        catch (Exception ex)
        {
            var errorTags = new TagList
            {
                { "mediator.request.type", MediatorTelemetryMetadata<TRequest, TResponse>.RequestType },
                { "mediator.request.kind", MediatorTelemetryMetadata<TRequest, TResponse>.RequestKind },
                { "error.type", ex.GetType().FullName! }
            };

            metrics.RequestErrors.Add(1, errorTags);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RequestDuration.Record(elapsed.TotalSeconds, tags);
            metrics.RequestActive.Add(-1, tags);
        }
    }
}
