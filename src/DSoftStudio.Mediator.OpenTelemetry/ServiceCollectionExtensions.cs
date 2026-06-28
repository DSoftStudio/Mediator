// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DSoftStudio.Mediator.OpenTelemetry;

public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenTelemetry instrumentation behaviors for the mediator.
    /// Call this after <c>AddMediator()</c> / <c>RegisterMediatorHandlers()</c>
    /// and before <c>PrecompilePipelines()</c>.
    /// </summary>
    public static IServiceCollection AddMediatorInstrumentation(
        this IServiceCollection services,
        Action<MediatorInstrumentationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MediatorInstrumentationOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // ── Pipeline behaviors ──────────────────────────────────────────

        if (options.EnableTracing)
        {
            // The request span is opened at the dispatch boundary through the core's observation port, so it
            // wraps the WHOLE pipeline — pre-/post-processors included (a behavior cannot: they run outside the
            // behavior chain). One stateless singleton adapter; the core injects it as IEnumerable and pays
            // nothing when it is absent. Streams have no dispatch port, so the stream span stays a behavior.
            services.AddSingleton<IMediatorDispatchObserver>(new MediatorDispatchTracingObserver(options));
            services.AddTransient(typeof(IStreamPipelineBehavior<,>), typeof(MediatorStreamTracingBehavior<,>));
        }

        if (options.EnableMetrics)
        {
            // The instruments are created from the DI IMeterFactory (Microsoft's prescribed pattern for a
            // DI-aware library — a static Meter cannot be isolated per service collection). AddMetrics() is
            // idempotent and registers the default IMeterFactory when the host has not already done so.
            services.AddMetrics();
            services.TryAddSingleton<MediatorMetrics>();

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(MediatorMetricsBehavior<,>));
            services.AddTransient(typeof(IStreamPipelineBehavior<,>), typeof(MediatorStreamMetricsBehavior<,>));
        }

        // ── Notification publisher decorator ────────────────────────────

        if (options.EnableTracing || options.EnableMetrics)
        {
            var existingDescriptor = FindLastDescriptor(services, typeof(INotificationPublisher));

            services.RemoveAll<INotificationPublisher>();

            services.AddSingleton<INotificationPublisher>(sp =>
            {
                var inner = ResolveInnerPublisher(sp, existingDescriptor);
                // MediatorMetrics is only registered when metrics are enabled — null here means tracing-only.
                return new InstrumentedNotificationPublisher(
                    inner,
                    sp.GetRequiredService<MediatorInstrumentationOptions>(),
                    sp.GetService<MediatorMetrics>());
            });
        }

        return services;
    }

    private static ServiceDescriptor? FindLastDescriptor(IServiceCollection services, Type serviceType)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == serviceType)
                return services[i];
        }
        return null;
    }

    private static INotificationPublisher ResolveInnerPublisher(
        IServiceProvider sp,
        ServiceDescriptor? descriptor)
    {
        if (descriptor is null)
            return new SequentialNotificationPublisher();

        if (descriptor.ImplementationInstance is INotificationPublisher instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (INotificationPublisher)descriptor.ImplementationFactory(sp);

        if (descriptor.ImplementationType is not null)
            return (INotificationPublisher)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);

        return new SequentialNotificationPublisher();
    }
}
