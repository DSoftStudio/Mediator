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
    /// and before <c>PrecompilePipelines()</c> — and before the other mediator packages
    /// (<c>AddMediatorFluentValidation()</c>, <c>AddMediatorHybridCache()</c>), since the scan that
    /// <c>PrecompilePipelines()</c> performs is a freeze point for all of them.
    /// <para>
    /// Calling this more than once is a no-op after the first: the first call's options win. Without
    /// that guard the second call decorates the first decorator, and every notification handler is
    /// then wrapped twice — the outer wrapper is what
    /// <c>mediator.handler.type</c> would report, so a consumer sees the wrapper instead of the
    /// subscriber, and one publish is reported as two.
    /// </para>
    /// <para>
    /// The observer registered here coexists with any the application registers of its own: every
    /// registered <see cref="IMediatorDispatchObserver"/> runs, and so does every
    /// <see cref="IMediatorNotificationObserver"/>.
    /// </para>
    /// </summary>
    public static IServiceCollection AddMediatorInstrumentation(
        this IServiceCollection services,
        Action<MediatorInstrumentationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (AlreadyRegistered(services))
            return services;

        var options = new MediatorInstrumentationOptions();
        configure?.Invoke(options);

        services.AddSingleton(new InstrumentationMarker());
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

            if (existingDescriptor is null)
            {
                // NOTHING to decorate, so do not invent something to decorate. Installing a publisher
                // here is what used to make enabling telemetry change the thing it observes: it
                // disarms the generated dispatch, resolves handlers from the container instead of the
                // compile-time table -- so a handler the generator could not see goes from skipped to
                // invoked -- and hands back a different singleton instance for a stateful handler.
                // The observation port produces the same spans without any of that.
                services.AddSingleton<IMediatorNotificationObserver>(sp =>
                    new MediatorNotificationTracingObserver(
                        sp.GetRequiredService<MediatorInstrumentationOptions>(),
                        sp.GetService<MediatorMetrics>()));
            }
            else
            {
                // The application registered its own publisher, so the fast path is ALREADY disarmed
                // and the membership already decided by the container. Decorating it changes nothing
                // that was not already changed, and it is the only way to reach the individual
                // handler invocations when a publisher owns the loop.
                services.RemoveAll<INotificationPublisher>();

                // The decorator keeps the ORIGINAL lifetime. Registering it Singleton promoted a
                // Scoped publisher to process-wide and resolved its dependencies from the root: under
                // ValidateScopes that throws, and without it a per-request publisher silently became
                // one instance for the life of the application.
                services.Add(new ServiceDescriptor(
                    typeof(INotificationPublisher),
                    sp =>
                    {
                        var inner = ResolveInnerPublisher(sp, existingDescriptor);
                        // MediatorMetrics is only registered when metrics are enabled — null here means tracing-only.
                        return new InstrumentedNotificationPublisher(
                            inner,
                            sp.GetRequiredService<MediatorInstrumentationOptions>(),
                            sp.GetService<MediatorMetrics>());
                    },
                    existingDescriptor.Lifetime));
            }
        }

        return services;
    }

    /// <summary>
    /// Marker that records this collection has already been instrumented. A descriptor rather than a
    /// flag because the collection is the only state shared across two calls.
    /// </summary>
    private sealed class InstrumentationMarker;

    private static bool AlreadyRegistered(IServiceCollection services)
    {
        for (int i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == typeof(InstrumentationMarker))
                return true;
        }
        return false;
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
