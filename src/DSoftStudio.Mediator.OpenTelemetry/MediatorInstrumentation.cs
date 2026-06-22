// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Provides the <see cref="ActivitySource"/> used by the mediator instrumentation, plus the shared name/version.
/// The metric instruments live on <see cref="MediatorMetrics"/> (created from the DI <c>IMeterFactory</c>).
/// </summary>
public static class MediatorInstrumentation
{
    /// <summary>
    /// The name used for both the <see cref="ActivitySource"/> and the metrics <c>Meter</c>.
    /// Use this constant when manually calling <c>AddSource()</c> or <c>AddMeter()</c>.
    /// </summary>
    public const string SourceName = "DSoftStudio.Mediator";

    /// <summary>The instrumentation version, stamped onto the <see cref="ActivitySource"/> and the metrics meter.</summary>
    internal static readonly string Version = typeof(MediatorInstrumentation)
        .Assembly.GetName().Version?.ToString() ?? "0.0.0";

    // The ActivitySource stays static + named (the .NET convention — there is no per-DI ActivitySource factory).
    internal static readonly ActivitySource ActivitySource = new(SourceName, Version);
}
