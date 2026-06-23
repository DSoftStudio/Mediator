// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: follows OpenTelemetry convention)

using DSoftStudio.Mediator.OpenTelemetry;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods for <see cref="TracerProviderBuilder"/> to add mediator instrumentation.
    /// </summary>
    public static class MediatorTracerProviderBuilderExtensions
    {
        /// <summary>
        /// Subscribes to the mediator <see cref="System.Diagnostics.ActivitySource"/> and installs the
        /// database span enricher.
        /// </summary>
        /// <remarks>
        /// In addition to <c>AddSource("DSoftStudio.Mediator")</c>, this registers
        /// <see cref="DatabaseSpanEnrichmentProcessor"/> so that any database client span flowing through
        /// the same provider is automatically tagged with a redaction-safe <c>db.operation.name</c> /
        /// <c>db.sql.table</c> when the underlying instrumentation only emitted a raw <c>db.statement</c>.
        /// That lets the Pipeline Explorer show each query (e.g. a <c>SELECT</c> vs an <c>INSERT</c>) as a
        /// distinct dependency instead of one aggregated row — with zero configuration. Call this before
        /// the exporter so the enrichment is applied prior to export.
        /// </remarks>
        public static TracerProviderBuilder AddMediatorInstrumentation(this TracerProviderBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder
                .AddSource(MediatorInstrumentation.SourceName)
                .AddProcessor(new DatabaseSpanEnrichmentProcessor());
        }
    }
}

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods for <see cref="MeterProviderBuilder"/> to add mediator instrumentation.
    /// </summary>
    public static class MediatorMeterProviderBuilderExtensions
    {
        /// <summary>
        /// Subscribes to the mediator <see cref="System.Diagnostics.Metrics.Meter"/>.
        /// Convenience method — equivalent to <c>AddMeter("DSoftStudio.Mediator")</c>.
        /// </summary>
        public static MeterProviderBuilder AddMediatorInstrumentation(this MeterProviderBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddMeter(MediatorInstrumentation.SourceName);
        }
    }
}
