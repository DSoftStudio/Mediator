// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Configuration options for mediator OpenTelemetry instrumentation.
/// </summary>
public sealed class MediatorInstrumentationOptions
{
    /// <summary>Gets or sets whether distributed tracing is enabled. Default: <c>true</c>.</summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>Gets or sets whether metrics collection is enabled. Default: <c>true</c>.</summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Optional filter. Return <c>false</c> to suppress instrumentation for the given request type.
    /// </summary>
    public Func<Type, bool>? Filter { get; set; }

    /// <summary>
    /// Optional enrichment callback. Add custom tags to the <see cref="Activity"/> for each request.
    /// The second parameter is the request object.
    /// </summary>
    public Action<Activity, object>? EnrichActivity { get; set; }

    /// <summary>
    /// Whether to include exception stack traces when recording exceptions on spans.
    /// Default: <c>true</c>.
    /// </summary>
    public bool RecordExceptionStackTraces { get; set; } = true;
}
