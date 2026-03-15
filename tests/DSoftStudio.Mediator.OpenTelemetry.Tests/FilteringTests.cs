// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class FilteringTests
{
    private readonly MeterListener _meterListener;
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterMeasurements = [];

    public FilteringTests()
    {
        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MediatorInstrumentation.SourceName)
                listener.EnableMeasurementEvents(instrument);
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

    public void Dispose() => _meterListener.Dispose();

    [Fact]
    public async Task Filter_suppresses_tracing_for_matched_request()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            Filter = type => !type.Name.StartsWith("HealthCheck")
        };
        var behavior = new MediatorTracingBehavior<HealthCheckQuery, string>(options);
        var handler = new HealthCheckHandler();

        var result = await behavior.Handle(new HealthCheckQuery(), handler, CancellationToken.None);

        result.ShouldBe("ok");
        collector.Activities.ShouldBeEmpty();
    }

    [Fact]
    public async Task Filter_allows_tracing_for_non_matched_request()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            Filter = type => !type.Name.StartsWith("HealthCheck")
        };
        var behavior = new MediatorTracingBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("test"), handler, CancellationToken.None);

        collector.Activities.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Filter_suppresses_metrics_for_matched_request()
    {
        var options = new MediatorInstrumentationOptions
        {
            Filter = type => !type.Name.StartsWith("HealthCheck")
        };
        var behavior = new MediatorMetricsBehavior<HealthCheckQuery, string>(options);
        var handler = new HealthCheckHandler();

        await behavior.Handle(new HealthCheckQuery(), handler, CancellationToken.None);
        _meterListener.RecordObservableInstruments();

        _measurements.ShouldBeEmpty();
        _counterMeasurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Filter_suppresses_stream_tracing()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            Filter = type => !type.Name.StartsWith("HealthCheck")
        };
        var behavior = new MediatorStreamTracingBehavior<HealthCheckStreamRequest, int>(options);
        var handler = new HealthCheckStreamHandler();

        await foreach (var _ in behavior.Handle(new HealthCheckStreamRequest(), handler, CancellationToken.None))
        { }

        collector.Activities.ShouldBeEmpty();
    }

    [Fact]
    public async Task Filter_suppresses_stream_metrics()
    {
        var options = new MediatorInstrumentationOptions
        {
            Filter = type => !type.Name.StartsWith("HealthCheck")
        };
        var behavior = new MediatorStreamMetricsBehavior<HealthCheckStreamRequest, int>(options);
        var handler = new HealthCheckStreamHandler();

        await foreach (var _ in behavior.Handle(new HealthCheckStreamRequest(), handler, CancellationToken.None))
        { }

        _meterListener.RecordObservableInstruments();

        _measurements.ShouldBeEmpty();
        _counterMeasurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Filter_suppresses_notification_tracing_and_metrics()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            Filter = type => !type.Name.StartsWith("HealthCheck")
        };
        var inner = new SequentialNotificationPublisher();
        var publisher = new InstrumentedNotificationPublisher(inner, options);

        var handlers = new INotificationHandler<HealthCheckNotification>[]
        {
            new HealthCheckNotificationHandler()
        };

        await publisher.Publish(handlers, new HealthCheckNotification(), CancellationToken.None);
        _meterListener.RecordObservableInstruments();

        collector.Activities.ShouldBeEmpty();
        _measurements.ShouldBeEmpty();
        _counterMeasurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Null_filter_instruments_everything()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions { Filter = null };
        var behavior = new MediatorTracingBehavior<HealthCheckQuery, string>(options);
        var handler = new HealthCheckHandler();

        await behavior.Handle(new HealthCheckQuery(), handler, CancellationToken.None);

        collector.Activities.ShouldHaveSingleItem();
    }
}
