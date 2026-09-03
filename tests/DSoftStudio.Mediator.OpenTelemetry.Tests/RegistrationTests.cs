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
    public void AddMediatorInstrumentation_registers_dispatch_tracing_observer()
    {
        // The request span is opened through the core's dispatch-observation port (so it wraps pre-/post-
        // processors, which a behavior cannot), NOT as a pipeline behavior.
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();

        var observer = services.SingleOrDefault(s => s.ServiceType == typeof(IMediatorDispatchObserver));

        observer.ShouldNotBeNull();
        observer!.ImplementationInstance.ShouldBeOfType<MediatorDispatchTracingObserver>();
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

        services.ShouldNotContain(s => s.ServiceType == typeof(IMediatorDispatchObserver));
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

    [Fact]
    public void AddMediatorInstrumentation_called_twice_registers_one_of_everything()
    {
        var services = new ServiceCollection();
        services.AddMediatorInstrumentation();
        services.AddMediatorInstrumentation();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<MediatorInstrumentationOptions>().Count().ShouldBe(1);
        provider.GetServices<IMediatorDispatchObserver>().Count().ShouldBe(1);

        // One layer of decoration. A second layer wraps every handler twice, and the OUTER wrapper is
        // what mediator.handler.type reports — InstrumentedHandler does not implement
        // IPipelineHandlerTypeAccessor, so the handler type cannot be resolved through it. A consumer
        // then sees the wrapper where the subscriber should be, and counts one publish as two.
        DecorationDepth(provider.GetRequiredService<INotificationPublisher>()).ShouldBe(1);
    }

    /// <summary>How many INotificationPublisher decorators are stacked, walking the inner chain.</summary>
    private static int DecorationDepth(INotificationPublisher publisher)
    {
        int depth = 0;
        object? current = publisher;

        while (current is not null && current.GetType().Name.Contains("Instrumented"))
        {
            depth++;
            var inner = current.GetType()
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .FirstOrDefault(f => typeof(INotificationPublisher).IsAssignableFrom(f.FieldType));
            current = inner?.GetValue(current);
        }

        return depth;
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
