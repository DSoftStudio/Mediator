// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class NotificationPublisherTests : IDisposable
{
    private readonly MeterListener _meterListener;
    private readonly TestMetrics _metrics = new();
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterMeasurements = [];

    public NotificationPublisherTests()
    {
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MediatorInstrumentation.SourceName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            _measurements.Add((instrument.Name, measurement, tags.ToArray()));
        });
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _counterMeasurements.Add((instrument.Name, measurement, tags.ToArray()));
        });
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _metrics.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Creates_parent_span_with_per_handler_child_spans()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();
        var handlers = new INotificationHandler<TestNotification>[] { handler1, handler2 };

        await publisher.Publish(handlers, new TestNotification("hello"), TestContext.Current.CancellationToken);

        handler1.Received.ShouldBe(["hello"]);
        handler2.Received.ShouldBe(["hello"]);

        // Should have 3 activities: parent + 2 handler child spans
        collector.Activities.Count.ShouldBe(3);

        var parentSpan = collector.Activities.Single(a => a.DisplayName == "TestNotification publish");
        parentSpan.Kind.ShouldBe(ActivityKind.Internal);
        parentSpan.Status.ShouldBe(ActivityStatusCode.Ok);
        parentSpan.GetTagItem("mediator.request.type")!.ShouldBe(typeof(TestNotification).FullName);
        parentSpan.GetTagItem("mediator.request.kind")!.ShouldBe("notification");

        var childSpans = collector.Activities
            .Where(a => a.DisplayName.EndsWith(" handle"))
            .ToList();
        childSpans.Count.ShouldBe(2);

        // Child spans should be children of the parent
        foreach (var child in childSpans)
        {
            child.ParentId.ShouldBe(parentSpan.Id);
            child.Status.ShouldBe(ActivityStatusCode.Ok);
        }
    }

    [Fact]
    public async Task Handler_child_span_names_use_handler_type_name()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handlers = new INotificationHandler<TestNotification>[]
        {
            new TestNotificationHandler1(),
            new TestNotificationHandler2()
        };

        await publisher.Publish(handlers, new TestNotification("test"), TestContext.Current.CancellationToken);

        var childNames = collector.Activities
            .Where(a => a.DisplayName.EndsWith(" handle"))
            .Select(a => a.DisplayName)
            .ToList();

        childNames.ShouldContain("TestNotificationHandler1 handle");
        childNames.ShouldContain("TestNotificationHandler2 handle");
    }

    [Fact]
    public async Task Handler_span_unwraps_transparent_wrapper_to_real_handler_type()
    {
        // Repro for the live-profiler + bridge coexistence bug: when the profiler wraps each notification
        // handler in a per-handler timing decorator (TimedNotificationHandler<T>, which implements
        // IPipelineHandlerTypeAccessor), the bridge must read the REAL handler type via that seam — NOT the
        // decorator's compiler-mangled type — so the notification fan-out shows the concrete subscriber
        // (SendConfirmationEmail/UpdateInventory), not <...>__TimedNotificationHandler.
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handlers = new INotificationHandler<TestNotification>[]
        {
            new TracingTransparentNotificationHandler<TestNotification>(new TestNotificationHandler1()),
            new TracingTransparentNotificationHandler<TestNotification>(new TestNotificationHandler2()) // file-scoped wrapper below
        };

        await publisher.Publish(handlers, new TestNotification("hi"), TestContext.Current.CancellationToken);

        var childSpans = collector.Activities
            .Where(a => a.DisplayName.EndsWith(" handle"))
            .ToList();
        childSpans.Count.ShouldBe(2);

        // Span NAME must use the real handler's type name, not the wrapper's mangled name.
        var childNames = childSpans.Select(a => a.DisplayName).ToList();
        childNames.ShouldContain("TestNotificationHandler1 handle");
        childNames.ShouldContain("TestNotificationHandler2 handle");

        // mediator.handler.type must be the REAL handler's FullName (what the IDE fan-out joins on).
        var handlerTypes = childSpans
            .Select(a => (string)a.GetTagItem("mediator.handler.type")!)
            .ToList();
        handlerTypes.ShouldContain(typeof(TestNotificationHandler1).FullName!);
        handlerTypes.ShouldContain(typeof(TestNotificationHandler2).FullName!);
        handlerTypes.ShouldNotContain(t => t.Contains("TracingTransparentNotificationHandler", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Error_in_handler_sets_error_status_on_parent_and_child()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handlers = new INotificationHandler<TestNotification>[]
        {
            new FailingNotificationHandler()
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await publisher.Publish(handlers, new TestNotification("boom"), TestContext.Current.CancellationToken));

        var parentSpan = collector.Activities.SingleOrDefault(a => a.DisplayName == "TestNotification publish");
        parentSpan.ShouldNotBeNull();
        parentSpan!.Status.ShouldBe(ActivityStatusCode.Error);
        parentSpan.GetTagItem("error.type")!.ShouldBe(typeof(InvalidOperationException).FullName);

        var childSpan = collector.Activities.SingleOrDefault(a => a.DisplayName.EndsWith(" handle"));
        childSpan.ShouldNotBeNull();
        childSpan!.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Records_metrics_for_notification_publish()
    {
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handlers = new INotificationHandler<TestNotification>[]
        {
            new TestNotificationHandler1()
        };

        await publisher.Publish(handlers, new TestNotification("test"), TestContext.Current.CancellationToken);
        _meterListener.RecordObservableInstruments();

        var duration = _measurements.Where(m => m.Name == "mediator.request.duration").ToList();
        duration.ShouldHaveSingleItem();
        duration[0].Tags.ShouldContain(t => t.Key == "mediator.request.kind" && (string)t.Value! == "notification");

        var active = _counterMeasurements.Where(m => m.Name == "mediator.request.active").ToList();
        active.Count.ShouldBe(2);
        active[0].Value.ShouldBe(1);
        active[1].Value.ShouldBe(-1);
    }

    [Fact]
    public async Task Records_error_metrics_on_handler_failure()
    {
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handlers = new INotificationHandler<TestNotification>[]
        {
            new FailingNotificationHandler()
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await publisher.Publish(handlers, new TestNotification("boom"), TestContext.Current.CancellationToken));

        _meterListener.RecordObservableInstruments();

        var errors = _counterMeasurements.Where(m => m.Name == "mediator.request.errors").ToList();
        errors.ShouldHaveSingleItem();
        errors[0].Tags.ShouldContain(t => t.Key == "error.type" && (string)t.Value! == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task Passthrough_when_no_listeners_and_no_meter()
    {
        // No ActivityCollector and no MeterListener → should pass through directly
        using var noMetrics = new MeterListener(); // empty, won't subscribe
        var options = new MediatorInstrumentationOptions();
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handler = new TestNotificationHandler1();
        var handlers = new INotificationHandler<TestNotification>[] { handler };

        await publisher.Publish(handlers, new TestNotification("test"), TestContext.Current.CancellationToken);

        handler.Received.ShouldBe(["test"]);
    }

    [Fact]
    public async Task EnrichActivity_callback_on_notification()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            EnrichActivity = (activity, request) =>
            {
                if (request is TestNotification n)
                    activity.SetTag("custom.value", n.Value);
            }
        };
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options, _metrics.Metrics);

        var handlers = new INotificationHandler<TestNotification>[]
        {
            new TestNotificationHandler1()
        };

        await publisher.Publish(handlers, new TestNotification("enriched"), TestContext.Current.CancellationToken);

        var parentSpan = collector.Activities.Single(a => a.DisplayName == "TestNotification publish");
        parentSpan.GetTagItem("custom.value")!.ShouldBe("enriched");
    }
}

/// <summary>
/// A per-handler notification decorator that is TRANSPARENT to tracing: it wraps an inner handler
/// (for timing/profiling) yet exposes the real handler type via <see cref="IPipelineHandlerTypeAccessor"/>,
/// exactly like the Enterprise live-profiler's generated per-handler wrapper (TimedNotificationHandler&lt;T&gt;).
/// <para>
/// Declared <c>file</c> so the OSS DependencyInjectionGenerator skips it during handler discovery — an
/// open-generic <c>INotificationHandler&lt;TNotification&gt;</c> shape would otherwise be registered with an
/// unbound type parameter. This is the SAME reason the real profiler wrapper is file-scoped.
/// </para>
/// </summary>
file sealed class TracingTransparentNotificationHandler<TNotification>(INotificationHandler<TNotification> inner)
    : INotificationHandler<TNotification>, IPipelineHandlerTypeAccessor
    where TNotification : INotification
{
    // Same recursive walk to the terminal handler as BehaviorHandlerAdapter / StreamPipelineChainHandler.
    public Type HandlerType
        => inner is IPipelineHandlerTypeAccessor a ? a.HandlerType : inner.GetType();

    public Task Handle(TNotification notification, CancellationToken cancellationToken)
        => inner.Handle(notification, cancellationToken);
}
