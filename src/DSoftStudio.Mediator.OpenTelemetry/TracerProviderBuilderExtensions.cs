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
        /// Subscribes to the mediator <see cref="System.Diagnostics.ActivitySource"/>.
        /// Convenience method — equivalent to <c>AddSource("DSoftStudio.Mediator")</c>.
        /// </summary>
        public static TracerProviderBuilder AddMediatorInstrumentation(this TracerProviderBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddSource(MediatorInstrumentation.SourceName);
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
