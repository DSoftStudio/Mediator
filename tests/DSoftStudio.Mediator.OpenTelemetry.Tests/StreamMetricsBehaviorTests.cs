// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class StreamMetricsBehaviorTests : IDisposable
{
    private readonly MeterListener _listener;
    private readonly TestMetrics _metrics = new();
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterMeasurements = [];

    public StreamMetricsBehaviorTests()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MediatorInstrumentation.SourceName)
                    listener.EnableMeasurementEvents(instrument);
            }
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

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A failing stream increments mediator.request.errors, tagged with what failed.
    /// </summary>
    /// <remarks>
    /// It never did. The behavior wrapped its enumeration in try/finally -- the only shape available,
    /// since C# forbids a catch clause in an iterator containing yield return -- so the exception went
    /// past unseen while the request-side twin has always recorded it. Measured by the reporter: five
    /// streams exercised, twenty-six measurements emitted, duration and active among them, and not one
    /// error. An operator watching error rate saw a clean line while every stream failed, and the
    /// metric contradicted the traces beside it.
    /// </remarks>
    [Fact]
    public async Task Records_an_error_when_the_stream_faults()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamMetricsBehavior<FailingStreamRequest, int>(options, _metrics.Metrics);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in behavior.Handle(
                new FailingStreamRequest(), new FailingStreamHandler(), TestContext.Current.CancellationToken))
            { }
        });

        var errors = _counterMeasurements.Where(m => m.Name == "mediator.request.errors").ToList();

        errors.ShouldHaveSingleItem();
        errors[0].Value.ShouldBe(1);

        var tags = errors[0].Tags.ToDictionary(t => t.Key, t => t.Value);
        tags["error.type"].ShouldBe(typeof(InvalidOperationException).FullName);
        tags["mediator.request.kind"].ShouldBe("stream");
        tags["mediator.request.type"].ShouldBe(typeof(FailingStreamRequest).FullName,
            "without the type dimension the counter cannot say WHICH stream is failing");
    }

    /// <summary>
    /// The other half: a stream that simply ends must not be counted as an error, or the counter
    /// becomes noise and the fix is worse than the bug.
    /// </summary>
    [Fact]
    public async Task Does_not_record_an_error_for_a_stream_that_completes()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options, _metrics.Metrics);

        await foreach (var _ in behavior.Handle(
            new TestStreamRequest(3), new TestStreamHandler(), TestContext.Current.CancellationToken))
        { }

        _counterMeasurements.Where(m => m.Name == "mediator.request.errors").ShouldBeEmpty();
    }


    [Fact]
    public async Task Records_duration_covering_full_enumeration()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options, _metrics.Metrics);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(3), handler, TestContext.Current.CancellationToken))
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
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options, _metrics.Metrics);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(1), handler, TestContext.Current.CancellationToken))
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
        var behavior = new MediatorStreamMetricsBehavior<FailingStreamRequest, int>(options, _metrics.Metrics);
        var handler = new FailingStreamHandler();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in behavior.Handle(new FailingStreamRequest(), handler, TestContext.Current.CancellationToken))
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
        var behavior = new MediatorStreamMetricsBehavior<TestStreamRequest, int>(options, _metrics.Metrics);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(1), handler, TestContext.Current.CancellationToken))
        { }

        _listener.RecordObservableInstruments();

        _measurements.ShouldBeEmpty();
        _counterMeasurements.ShouldBeEmpty();
    }
}
