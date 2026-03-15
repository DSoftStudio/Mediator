// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class StreamMetricsBehaviorTests
{
    private readonly MeterListener _listener;
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterMeasurements = [];

    public StreamMetricsBehaviorTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MediatorInstrumentation.SourceName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            _measurements.Add((instrument.Name, measurement, tags.ToArray()));
        });
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _counterMeasurements.Add((instrument.Name, measurement, tags.ToArray()));
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task Records_duration_covering_full_enumeration()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(3), handler, CancellationToken.None))
        { }

        _listener.RecordObservableInstruments();

        var duration = _measurements.Where(m => m.Name == "mediator.request.duration").ToList();
        duration.ShouldHaveSingleItem();
        duration[0].Value.ShouldBeGreaterThanOrEqualTo(0);

        var tags = duration[0].Tags;
        tags.ShouldContain(t => t.Key == "mediator.request.type" && (string)t.Value! == typeof(TestStreamRequest).FullName);
        tags.ShouldContain(t => t.Key == "mediator.request.kind" && (string)t.Value! == "stream");
    }

    [Fact]
    public async Task Records_active_count_for_stream()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(1), handler, CancellationToken.None))
        { }

        _listener.RecordObservableInstruments();

        var active = _counterMeasurements.Where(m => m.Name == "mediator.request.active").ToList();
        active.Count.ShouldBe(2);
        active[0].Value.ShouldBe(1);  // increment
        active[1].Value.ShouldBe(-1); // decrement
    }

    [Fact]
    public async Task Records_duration_even_on_stream_error()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamMetricsBehavior<FailingStreamRequest, int>(options);
        var handler = new FailingStreamHandler();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in behavior.Handle(new FailingStreamRequest(), handler, CancellationToken.None))
            { }
        });

        _listener.RecordObservableInstruments();

        // Duration should still be recorded in the finally block
        var duration = _measurements.Where(m => m.Name == "mediator.request.duration").ToList();
        duration.ShouldHaveSingleItem();
        duration[0].Value.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task No_metrics_when_disabled()
    {
        var options = new MediatorInstrumentationOptions { EnableMetrics = false };
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(1), handler, CancellationToken.None))
        { }

        _listener.RecordObservableInstruments();

        _measurements.ShouldBeEmpty();
        _counterMeasurements.ShouldBeEmpty();
    }
}
