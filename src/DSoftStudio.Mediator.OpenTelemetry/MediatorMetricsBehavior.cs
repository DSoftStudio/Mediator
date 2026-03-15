// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Pipeline behavior that records metrics (duration, active count, errors) for mediator requests.
/// </summary>
public sealed class MediatorMetricsBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!options.EnableMetrics || !MediatorInstrumentation.RequestDuration.Enabled)
            return await next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return await next.Handle(request, cancellationToken);

        var tags = new TagList
        {
            { "mediator.request.type", MediatorTelemetryMetadata<TRequest, TResponse>.RequestType },
            { "mediator.request.kind", MediatorTelemetryMetadata<TRequest, TResponse>.RequestKind }
        };

        MediatorInstrumentation.RequestActive.Add(1, tags);
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

            MediatorInstrumentation.RequestErrors.Add(1, errorTags);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            MediatorInstrumentation.RequestDuration.Record(elapsed.TotalSeconds, tags);
            MediatorInstrumentation.RequestActive.Add(-1, tags);
        }
    }
}
