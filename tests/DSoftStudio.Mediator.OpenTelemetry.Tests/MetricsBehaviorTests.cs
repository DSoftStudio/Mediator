// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class MetricsBehaviorTests
{
    private readonly MeterListener _listener;
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _counterMeasurements = [];

    public MetricsBehaviorTests()
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
    public async Task Records_duration_on_success()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorMetricsBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("test"), handler, CancellationToken.None);
        _listener.RecordObservableInstruments();

        var duration = _measurements.Where(m => m.Name == "mediator.request.duration").ToList();
        duration.ShouldHaveSingleItem();
        duration[0].Value.ShouldBeGreaterThanOrEqualTo(0);

        var tags = duration[0].Tags;
        tags.ShouldContain(t => t.Key == "mediator.request.type" && (string)t.Value! == typeof(TestCommand).FullName);
        tags.ShouldContain(t => t.Key == "mediator.request.kind" && (string)t.Value! == "command");
    }

    [Fact]
    public async Task Records_active_count_increment_and_decrement()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorMetricsBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("test"), handler, CancellationToken.None);
        _listener.RecordObservableInstruments();

        var active = _counterMeasurements.Where(m => m.Name == "mediator.request.active").ToList();
        active.Count.ShouldBe(2);
        active[0].Value.ShouldBe(1);  // increment
        active[1].Value.ShouldBe(-1); // decrement
    }

    [Fact]
    public async Task Records_error_count_on_exception()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorMetricsBehavior<FailingCommand, string>(options);
        var handler = new FailingCommandHandler();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FailingCommand("boom"), handler, CancellationToken.None));

        _listener.RecordObservableInstruments();

        var errors = _counterMeasurements.Where(m => m.Name == "mediator.request.errors").ToList();
        errors.ShouldHaveSingleItem();
        errors[0].Tags.ShouldContain(t => t.Key == "error.type" && (string)t.Value! == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task No_metrics_when_disabled()
    {
        var options = new MediatorInstrumentationOptions { EnableMetrics = false };
        var behavior = new MediatorMetricsBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("test"), handler, CancellationToken.None);
        _listener.RecordObservableInstruments();

        _measurements.ShouldBeEmpty();
        _counterMeasurements.ShouldBeEmpty();
    }
}
