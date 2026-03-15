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

            options.EnrichActivity?.Invoke(activity, request);
        }

        bool success = false;
        try
        {
            await foreach (var item in next.Handle(request, cancellationToken).WithCancellation(cancellationToken))
            {
                yield return item;
            }
            success = true;
        }
        finally
        {
            activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
    }
}
