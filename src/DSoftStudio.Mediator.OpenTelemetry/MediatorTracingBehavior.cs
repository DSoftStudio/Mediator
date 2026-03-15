// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Pipeline behavior that creates distributed tracing spans for mediator requests.
/// Registers as the outermost behavior to capture the full pipeline duration.
/// </summary>
public sealed class MediatorTracingBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!options.EnableTracing || !Source.HasListeners())
            return await next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return await next.Handle(request, cancellationToken);

        using var activity = Source.StartActivity(
            MediatorTelemetryMetadata<TRequest, TResponse>.SpanName,
            ActivityKind.Internal);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("mediator.request.type", MediatorTelemetryMetadata<TRequest, TResponse>.RequestType);
            activity.SetTag("mediator.response.type", MediatorTelemetryMetadata<TRequest, TResponse>.ResponseType);
            activity.SetTag("mediator.request.kind", MediatorTelemetryMetadata<TRequest, TResponse>.RequestKind);

            options.EnrichActivity?.Invoke(activity, request);
        }

        try
        {
            var response = await next.Handle(request, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag("error.type", ex.GetType().FullName);
                ActivityHelper.RecordException(activity, ex, options.RecordExceptionStackTraces);
            }
            throw;
        }
    }
}
