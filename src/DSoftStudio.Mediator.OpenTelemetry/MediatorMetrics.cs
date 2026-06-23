// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Owns the mediator metric instruments. The <see cref="Meter"/> is created from the DI
/// <see cref="IMeterFactory"/> — the pattern Microsoft prescribes for a DI-aware library, because a
/// <c>static</c> <see cref="Meter"/> cannot be isolated per service collection (it leaks measurements across
/// parallel tests and across hosts in the same process). Registered as a singleton by
/// <c>AddMediatorInstrumentation()</c>; the meter name is <see cref="MediatorInstrumentation.SourceName"/>, so an
/// app still subscribes with the same <c>AddMeter("DSoftStudio.Mediator")</c> call.
/// </summary>
public sealed class MediatorMetrics
{
    // Explicit sub-second bucket boundaries (in SECONDS) so the duration histogram yields meaningful p50/p95/p99
    // for millisecond-scale mediator requests. Without this, the OpenTelemetry default buckets ([0, 5, 10, 25, …]
    // seconds) collapse every sub-5-second request into the first bucket → useless percentiles. Supplied via
    // InstrumentAdvice, which the OpenTelemetry .NET SDK (>= 1.10) honours as the default boundaries.
    private static readonly double[] DurationSecondsBuckets =
        [0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    public MediatorMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MediatorInstrumentation.SourceName, MediatorInstrumentation.Version);

        RequestDuration = meter.CreateHistogram<double>(
            name: "mediator.request.duration",
            unit: "s",
            description: "Time from behavior entry to handler completion",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationSecondsBuckets });

        RequestActive = meter.CreateUpDownCounter<long>(
            name: "mediator.request.active",
            unit: "{request}",
            description: "Number of in-flight requests");

        RequestErrors = meter.CreateCounter<long>(
            name: "mediator.request.errors",
            unit: "{error}",
            description: "Count of failed requests");
    }

    /// <summary>Histogram of request durations in SECONDS (record with <c>elapsed.TotalSeconds</c>).</summary>
    public Histogram<double> RequestDuration { get; }

    /// <summary>In-flight request count (+1 on entry, −1 on completion).</summary>
    public UpDownCounter<long> RequestActive { get; }

    /// <summary>Count of failed requests, tagged with <c>error.type</c>.</summary>
    public Counter<long> RequestErrors { get; }
}
