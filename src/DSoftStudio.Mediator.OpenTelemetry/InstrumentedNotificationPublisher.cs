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
internal sealed class InstrumentedNotificationPublisher : INotificationPublisher
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;
    private static readonly ConcurrentDictionary<Type, string> HandlerSpanNames = new();

    private readonly INotificationPublisher _inner;
    private readonly MediatorInstrumentationOptions _options;

    public InstrumentedNotificationPublisher(INotificationPublisher inner, MediatorInstrumentationOptions options)
    {
        _inner = inner;
        _options = options;
    }

    public async Task Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        bool tracingActive = _options.EnableTracing && Source.HasListeners();
        bool metricsActive = _options.EnableMetrics && MediatorInstrumentation.RequestDuration.Enabled;

        if (!tracingActive && !metricsActive)
        {
            await _inner.Publish(handlers, notification, cancellationToken);
            return;
        }

        if (_options.Filter is not null && !_options.Filter(typeof(TNotification)))
        {
            await _inner.Publish(handlers, notification, cancellationToken);
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

                _options.EnrichActivity?.Invoke(parentActivity, notification);
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
                ? handlers.Select(h => new InstrumentedHandler<TNotification>(h, _options))
                : handlers;

            await _inner.Publish(effectiveHandlers, notification, cancellationToken);
            parentActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            if (parentActivity is not null)
            {
                parentActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
                parentActivity.SetTag("error.type", ex.GetType().FullName);
                ActivityHelper.RecordException(parentActivity, ex, _options.RecordExceptionStackTraces);
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
    private sealed class InstrumentedHandler<TNotification> : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        private readonly INotificationHandler<TNotification> _inner;
        private readonly MediatorInstrumentationOptions _options;

        public InstrumentedHandler(INotificationHandler<TNotification> inner, MediatorInstrumentationOptions options)
        {
            _inner = inner;
            _options = options;
        }

        public async Task Handle(TNotification notification, CancellationToken cancellationToken)
        {
            var spanName = HandlerSpanNames.GetOrAdd(
                _inner.GetType(),
                static type => $"{type.Name} handle");

            using var activity = Source.StartActivity(spanName, ActivityKind.Internal);

            try
            {
                await _inner.Handle(notification, cancellationToken);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                if (activity is not null)
                {
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity.SetTag("error.type", ex.GetType().FullName);
                    ActivityHelper.RecordException(activity, ex, _options.RecordExceptionStackTraces);
                }
                throw;
            }
        }
    }
}
