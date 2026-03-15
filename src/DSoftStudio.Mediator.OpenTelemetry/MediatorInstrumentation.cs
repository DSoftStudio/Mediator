// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Provides the <see cref="ActivitySource"/> and <see cref="Meter"/> used by the mediator instrumentation.
/// </summary>
public static class MediatorInstrumentation
{
    /// <summary>
    /// The name used for both the <see cref="ActivitySource"/> and <see cref="Meter"/>.
    /// Use this constant when manually calling <c>AddSource()</c> or <c>AddMeter()</c>.
    /// </summary>
    public const string SourceName = "DSoftStudio.Mediator";

    private static readonly string Version = typeof(MediatorInstrumentation)
        .Assembly.GetName().Version?.ToString() ?? "0.0.0";

    internal static readonly ActivitySource ActivitySource = new(SourceName, Version);
    internal static readonly Meter Meter = new(SourceName, Version);

    // ── Metric instruments ─────────────────────────────────────────────

    internal static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("mediator.request.duration", "s",
            "Time from behavior entry to handler completion");

    internal static readonly UpDownCounter<long> RequestActive =
        Meter.CreateUpDownCounter<long>("mediator.request.active", "{request}",
            "Number of in-flight requests");

    internal static readonly Counter<long> RequestErrors =
        Meter.CreateCounter<long>("mediator.request.errors", "{error}",
            "Count of failed requests");
}
