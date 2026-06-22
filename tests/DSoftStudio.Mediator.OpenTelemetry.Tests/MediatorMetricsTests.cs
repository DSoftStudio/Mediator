// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

/// <summary>
/// Verifies the metric instruments follow the .NET / OpenTelemetry standards: created from an
/// <see cref="IMeterFactory"/> (not a static Meter), in SECONDS, with explicit sub-second histogram bucket
/// boundaries so p50/p95/p99 are meaningful for millisecond-scale requests (the OTel default buckets would
/// collapse every sub-5-second request into one bucket).
/// </summary>
[Collection("OTel")]
public class MediatorMetricsTests
{
    [Fact]
    public void Duration_histogram_advertises_sub_second_buckets_not_the_otel_defaults()
    {
        var exported = new List<Metric>();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new MediatorMetrics(provider.GetRequiredService<IMeterFactory>());

        using var meterProvider = global::OpenTelemetry.Sdk.CreateMeterProviderBuilder()
            .AddMeter(MediatorInstrumentation.SourceName)
            .AddInMemoryExporter(exported)
            .Build();

        // A typical 8 ms request, recorded in seconds.
        metrics.RequestDuration.Record(0.008);
        meterProvider!.ForceFlush();

        var histogram = exported.Single(m => m.Name == "mediator.request.duration");
        histogram.MetricType.ShouldBe(MetricType.Histogram);
        histogram.Unit.ShouldBe("s");

        var bounds = new List<double>();
        foreach (ref readonly var point in histogram.GetMetricPoints())
        {
            foreach (var bucket in point.GetHistogramBuckets())
                if (!double.IsPositiveInfinity(bucket.ExplicitBound))
                    bounds.Add(bucket.ExplicitBound);
            break;
        }

        // Our explicit sub-second boundaries — NOT the OTel default [0,5,10,25,…] s.
        bounds.Count.ShouldBe(13);
        bounds.ShouldContain(0.005);
        bounds.ShouldContain(0.01);
        bounds.ShouldContain(0.5);
        bounds.ShouldNotContain(25); // a default-bucket boundary that must be absent
    }

    [Fact]
    public void Instruments_are_named_and_unit_per_convention()
    {
        var names = new HashSet<string>();
        var units = new Dictionary<string, string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != MediatorInstrumentation.SourceName) return;
                names.Add(instrument.Name);
                units[instrument.Name] = instrument.Unit;
            }
        };
        listener.Start();

        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _ = new MediatorMetrics(provider.GetRequiredService<IMeterFactory>());

        names.ShouldBe(new[] { "mediator.request.duration", "mediator.request.active", "mediator.request.errors" }, ignoreOrder: true);
        units["mediator.request.duration"].ShouldBe("s");          // UCUM seconds
        units["mediator.request.active"].ShouldBe("{request}");    // dimensionless annotation
        units["mediator.request.errors"].ShouldBe("{error}");
    }
}
