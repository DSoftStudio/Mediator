// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Produces the publish envelope span, one span per subscriber, and the notification metrics —
/// through the core's observation port instead of by replacing the registered
/// <see cref="INotificationPublisher"/>.
/// <para>
/// The spans are identical to the ones the decorator emitted, deliberately: consumers classify a
/// notification by <c>mediator.request.kind</c>, tell a publish envelope from a subscriber by whether
/// <c>mediator.handler.type</c> is present, and pair the fan-out with its publish through the
/// parent/child relationship. None of that may move.
/// </para>
/// </summary>
internal sealed class MediatorNotificationTracingObserver(
    MediatorInstrumentationOptions options,
    MediatorMetrics? metrics) : IMediatorNotificationObserver
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;
    private static readonly ConcurrentDictionary<Type, string> HandlerSpanNames = new();

    /// <summary>
    /// True when EITHER gate is live. They are ORed rather than ANDed on purpose: tracing and metrics
    /// are independent switches, and collapsing them would silently drop notification metrics for a
    /// metrics-only user.
    /// </summary>
    public bool IsActive
        => (options.EnableTracing && Source.HasListeners())
           || (options.EnableMetrics && metrics is not null && metrics.RequestDuration.Enabled);

    public IMediatorPublishScope? BeginPublish<TNotification>(TNotification notification)
        where TNotification : INotification
    {
        if (options.Filter is not null && !options.Filter(typeof(TNotification)))
            return null;

        bool tracing = options.EnableTracing && Source.HasListeners();
        bool measuring = options.EnableMetrics && metrics is not null && metrics.RequestDuration.Enabled;

        Activity? envelope = null;
        if (tracing)
        {
            envelope = Source.StartActivity(
                MediatorNotificationMetadata<TNotification>.SpanName,
                ActivityKind.Internal);

            if (envelope is { IsAllDataRequested: true })
            {
                envelope.SetTag("mediator.request.type", MediatorNotificationMetadata<TNotification>.RequestType);
                envelope.SetTag("mediator.request.kind", MediatorNotificationMetadata<TNotification>.RequestKind);

                // Deliberately NO mediator.handler.type here: its ABSENCE is what marks this span as
                // the publish envelope rather than one subscriber's execution.
                options.EnrichActivity?.Invoke(envelope, notification);
            }
        }

        TagList tags = default;
        long startTimestamp = 0;
        if (measuring)
        {
            tags = new TagList
            {
                { "mediator.request.type", MediatorNotificationMetadata<TNotification>.RequestType },
                { "mediator.request.kind", MediatorNotificationMetadata<TNotification>.RequestKind }
            };

            metrics!.RequestActive.Add(1, tags);
            startTimestamp = Stopwatch.GetTimestamp();
        }

        if (envelope is null && !measuring)
            return null;

        return new PublishScope(envelope, options, metrics, measuring, tags, startTimestamp);
    }

    private sealed class PublishScope(
        Activity? envelope,
        MediatorInstrumentationOptions options,
        MediatorMetrics? metrics,
        bool measuring,
        TagList tags,
        long startTimestamp) : IMediatorPublishScope
    {
        public IMediatorSubscriberScope? BeginSubscriber(object handler)
        {
            if (envelope is null)
                return null; // metrics-only: nothing per subscriber to emit

            // Resolve the CONCRETE subscriber through any transparent decorator — the live profiler
            // wraps handlers and exposes the real one this way. Reading the wrapper instead would
            // collapse every subscriber to one type and break the per-handler join.
            var handlerType = handler is IPipelineHandlerTypeAccessor accessor
                ? accessor.HandlerType
                : handler.GetType();

            // Anchor AMBIENTLY, not with an explicit ActivityContext. Both parent the span to the
            // envelope, but an explicit context starts a new trace-local scope and drops Baggage —
            // every outgoing baggage header and every log enrichment reading Activity.Current.Baggage
            // inside the subscriber would go empty.
            var previous = Activity.Current;
            Activity.Current = envelope;

            var span = Source.StartActivity(
                HandlerSpanNames.GetOrAdd(handlerType, static type => $"{type.Name} handle"),
                ActivityKind.Internal);

            if (span is null)
            {
                Activity.Current = previous;
                return null;
            }

            if (span.IsAllDataRequested)
            {
                span.SetTag("mediator.request.kind", envelope.GetTagItem("mediator.request.kind"));
                span.SetTag("mediator.request.type", envelope.GetTagItem("mediator.request.type"));
                span.SetTag("mediator.handler.type", handlerType.FullName);
            }

            return new SubscriberScope(span, previous, options);
        }

        public void OnError(Exception exception)
        {
            if (envelope is not null)
            {
                envelope.SetStatus(ActivityStatusCode.Error, exception.Message);
                envelope.SetTag("error.type", exception.GetType().FullName);
                ActivityHelper.RecordException(envelope, exception, options.RecordExceptionStackTraces);
            }

            if (measuring)
            {
                var errorTags = new TagList
                {
                    { "mediator.request.type", envelope?.GetTagItem("mediator.request.type") },
                    { "mediator.request.kind", "notification" },
                    { "error.type", exception.GetType().FullName! }
                };

                metrics!.RequestErrors.Add(1, errorTags);
            }
        }

        public void Dispose()
        {
            if (envelope is not null && envelope.Status != ActivityStatusCode.Error)
                envelope.SetStatus(ActivityStatusCode.Ok);

            envelope?.Dispose();

            if (measuring)
            {
                var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                metrics!.RequestDuration.Record(elapsed.TotalSeconds, tags);
                metrics!.RequestActive.Add(-1, tags);
            }
        }
    }

    private sealed class SubscriberScope(
        Activity span,
        Activity? previous,
        MediatorInstrumentationOptions options) : IMediatorSubscriberScope
    {
        public void OnError(Exception exception)
        {
            span.SetStatus(ActivityStatusCode.Error, exception.Message);
            span.SetTag("error.type", exception.GetType().FullName);
            ActivityHelper.RecordException(span, exception, options.RecordExceptionStackTraces);
        }

        public void Dispose()
        {
            if (span.Status != ActivityStatusCode.Error)
                span.SetStatus(ActivityStatusCode.Ok);

            span.Dispose();

            // Put back exactly what was ambient before this subscriber, rather than trusting
            // Dispose's own restore: the handler may have left something of its own current.
            Activity.Current = previous;
        }
    }
}
