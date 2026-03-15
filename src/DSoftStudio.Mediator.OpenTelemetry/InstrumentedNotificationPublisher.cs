// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Decorator that wraps an <see cref="INotificationPublisher"/> with distributed tracing
/// and metrics. Creates a parent span for the publish operation and per-handler child spans.
/// </summary>
internal sealed class InstrumentedNotificationPublisher(INotificationPublisher inner, MediatorInstrumentationOptions options) : INotificationPublisher
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;
    private static readonly ConcurrentDictionary<Type, string> HandlerSpanNames = new();

    public async Task Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        bool tracingActive = options.EnableTracing && Source.HasListeners();
        bool metricsActive = options.EnableMetrics && MediatorInstrumentation.RequestDuration.Enabled;

        if (!tracingActive && !metricsActive)
        {
            await inner.Publish(handlers, notification, cancellationToken);
            return;
        }

        if (options.Filter is not null && !options.Filter(typeof(TNotification)))
        {
            await inner.Publish(handlers, notification, cancellationToken);
            return;
        }

        // ── Tracing: parent span ──────────────────────────────────────
        Activity? parentActivity = null;
        if (tracingActive)
        {
            parentActivity = Source.StartActivity(
                MediatorNotificationMetadata<TNotification>.SpanName,
                ActivityKind.Internal);

            if (parentActivity is { IsAllDataRequested: true })
            {
                parentActivity.SetTag("mediator.request.type", MediatorNotificationMetadata<TNotification>.RequestType);
                parentActivity.SetTag("mediator.request.kind", MediatorNotificationMetadata<TNotification>.RequestKind);

                options.EnrichActivity?.Invoke(parentActivity, notification);
            }
        }

        // ── Metrics: active count + timing ────────────────────────────
        TagList metricTags = default;
        long startTimestamp = 0;
        if (metricsActive)
        {
            metricTags = new TagList
            {
                { "mediator.request.type", MediatorNotificationMetadata<TNotification>.RequestType },
                { "mediator.request.kind", MediatorNotificationMetadata<TNotification>.RequestKind }
            };

            MediatorInstrumentation.RequestActive.Add(1, metricTags);
            startTimestamp = Stopwatch.GetTimestamp();
        }

        try
        {
            var effectiveHandlers = tracingActive
                ? handlers.Select(h => new InstrumentedHandler<TNotification>(h, options))
                : handlers;

            await inner.Publish(effectiveHandlers, notification, cancellationToken);
            parentActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            if (parentActivity is not null)
            {
                parentActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
                parentActivity.SetTag("error.type", ex.GetType().FullName);
                ActivityHelper.RecordException(parentActivity, ex, options.RecordExceptionStackTraces);
            }

            if (metricsActive)
            {
                var errorTags = new TagList
                {
                    { "mediator.request.type", MediatorNotificationMetadata<TNotification>.RequestType },
                    { "mediator.request.kind", MediatorNotificationMetadata<TNotification>.RequestKind },
                    { "error.type", ex.GetType().FullName! }
                };

                MediatorInstrumentation.RequestErrors.Add(1, errorTags);
            }

            throw;
        }
        finally
        {
            parentActivity?.Dispose();

            if (metricsActive)
            {
                var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                MediatorInstrumentation.RequestDuration.Record(elapsed.TotalSeconds, metricTags);
                MediatorInstrumentation.RequestActive.Add(-1, metricTags);
            }
        }
    }

    /// <summary>
    /// Wrapper that creates a child span for each notification handler invocation.
    /// </summary>
    private sealed class InstrumentedHandler<TNotification>(INotificationHandler<TNotification> inner, MediatorInstrumentationOptions options) : INotificationHandler<TNotification>
        where TNotification : INotification
    {

        public async Task Handle(TNotification notification, CancellationToken cancellationToken)
        {
            var spanName = HandlerSpanNames.GetOrAdd(
                inner.GetType(),
                static type => $"{type.Name} handle");

            using var activity = Source.StartActivity(spanName, ActivityKind.Internal);

            try
            {
                await inner.Handle(notification, cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Ok);
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
}
