// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class RegistrationTests
{
    [Fact]
    public void AddMediatorInstrumentation_registers_tracing_behaviors()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();

        var pipelineBehaviors = services
            .Where(s => s.ServiceType == typeof(IPipelineBehavior<,>))
            .ToList();

        pipelineBehaviors.ShouldContain(s =>
            s.ImplementationType == typeof(MediatorTracingBehavior<,>));
    }

    [Fact]
    public void AddMediatorInstrumentation_registers_metrics_behaviors()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();

        var pipelineBehaviors = services
            .Where(s => s.ServiceType == typeof(IPipelineBehavior<,>))
            .ToList();

        pipelineBehaviors.ShouldContain(s =>
            s.ImplementationType == typeof(MediatorMetricsBehavior<,>));
    }

    [Fact]
    public void AddMediatorInstrumentation_registers_stream_behaviors()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();

        var streamBehaviors = services
            .Where(s => s.ServiceType == typeof(IStreamPipelineBehavior<,>))
            .ToList();

        streamBehaviors.ShouldContain(s =>
            s.ImplementationType == typeof(MediatorStreamTracingBehavior<,>));
        streamBehaviors.ShouldContain(s =>
            s.ImplementationType == typeof(MediatorStreamMetricsBehavior<,>));
    }

    [Fact]
    public void AddMediatorInstrumentation_registers_notification_publisher_decorator()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();

        var publisherDescriptor = services
            .LastOrDefault(s => s.ServiceType == typeof(INotificationPublisher));

        publisherDescriptor.ShouldNotBeNull();
        publisherDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);

        // Should resolve to InstrumentedNotificationPublisher
        var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<INotificationPublisher>();
        publisher.ShouldBeOfType<InstrumentedNotificationPublisher>();
    }

    [Fact]
    public void AddMediatorInstrumentation_wraps_existing_custom_publisher()
    {
        var services = new ServiceCollection();
        var customPublisher = new TestCustomPublisher();
        services.AddSingleton<INotificationPublisher>(customPublisher);

        services.AddMediatorInstrumentation();

        var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<INotificationPublisher>();
        publisher.ShouldBeOfType<InstrumentedNotificationPublisher>();
    }

    [Fact]
    public void AddMediatorInstrumentation_wraps_default_sequential_publisher_when_none_registered()
    {
        var services = new ServiceCollection();
        // No custom publisher registered

        services.AddMediatorInstrumentation();

        var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<INotificationPublisher>();
        publisher.ShouldBeOfType<InstrumentedNotificationPublisher>();
    }

    [Fact]
    public void Disabling_tracing_skips_tracing_behaviors()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation(options =>
        {
            options.EnableTracing = false;
        });

        services.ShouldNotContain(s =>
            s.ImplementationType == typeof(MediatorTracingBehavior<,>));
        services.ShouldNotContain(s =>
            s.ImplementationType == typeof(MediatorStreamTracingBehavior<,>));
    }

    [Fact]
    public void Disabling_metrics_skips_metrics_behaviors()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation(options =>
        {
            options.EnableMetrics = false;
        });

        services.ShouldNotContain(s =>
            s.ImplementationType == typeof(MediatorMetricsBehavior<,>));
        services.ShouldNotContain(s =>
            s.ImplementationType == typeof(MediatorStreamMetricsBehavior<,>));
    }

    [Fact]
    public void Options_are_registered_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation(options =>
        {
            options.RecordExceptionStackTraces = false;
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<MediatorInstrumentationOptions>();
        options.RecordExceptionStackTraces.ShouldBeFalse();
    }

    // ── Test helpers ──────────────────────────────────────────────────

    private sealed class TestCustomPublisher : INotificationPublisher
    {
        public Task Publish<TNotification>(
            IEnumerable<INotificationHandler<TNotification>> handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
