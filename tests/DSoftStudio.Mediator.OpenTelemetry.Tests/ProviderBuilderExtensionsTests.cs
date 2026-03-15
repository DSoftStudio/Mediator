// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class ProviderBuilderExtensionsTests
{
    [Fact]
    public void AddMediatorInstrumentation_tracer_returns_builder()
    {
        using var tracerProvider = global::OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .AddMediatorInstrumentation()
            .Build();

        tracerProvider.ShouldNotBeNull();
    }

    [Fact]
    public void AddMediatorInstrumentation_tracer_throws_on_null()
    {
        TracerProviderBuilder builder = null!;
        Should.Throw<ArgumentNullException>(() => builder.AddMediatorInstrumentation());
    }

    [Fact]
    public void AddMediatorInstrumentation_meter_returns_builder()
    {
        using var meterProvider = global::OpenTelemetry.Sdk.CreateMeterProviderBuilder()
            .AddMediatorInstrumentation()
            .Build();

        meterProvider.ShouldNotBeNull();
    }

    [Fact]
    public void AddMediatorInstrumentation_meter_throws_on_null()
    {
        MeterProviderBuilder builder = null!;
        Should.Throw<ArgumentNullException>(() => builder.AddMediatorInstrumentation());
    }
}
