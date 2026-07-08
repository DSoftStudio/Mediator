// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Two-phase, order-independent optimizer that raises an auto-detected request-handler lifetime from
    /// the conservative Transient default to the longest <i>safe</i> lifetime allowed by its constructor
    /// dependencies:
    /// <list type="bullet">
    /// <item><description><b>Singleton</b> when every dependency is a singleton - cached, zero-allocation
    /// per request (and, via the pipeline-chain lifetime fold, a cached Singleton chain too).</description></item>
    /// <item><description><b>Scoped</b> when any dependency is scoped - cached per scope; the handler can
    /// only be resolved inside a scope anyway, so this adds no constraint.</description></item>
    /// <item><description><b>Transient</b> (unchanged) when any dependency is itself transient or
    /// unregistered - it is not safe to capture for longer.</description></item>
    /// </list>
    /// <para>
    /// <b>Why two phases.</b> The generated <c>RegisterMediatorHandlers()</c> calls <see cref="Stage"/> to
    /// record the descriptors it created for dependency-carrying handlers (by reference) plus their
    /// dependency types. The decision itself is DEFERRED to <see cref="Apply"/>, which the generated
    /// finalization step (<c>PrecompilePipelines()</c> / the single-call <c>AddMediator(configure)</c>) runs
    /// once every registration is present - so a dependency registered AFTER the handler (the common
    /// composition-root order: <c>RegisterMediatorHandlers()</c> first, repositories/<c>DbContext</c> after)
    /// is still seen. Running before the pipeline chains are registered lets the chain lifetime fold observe
    /// the upgraded value.
    /// </para>
    /// <para>
    /// No reflection - only registered <see cref="ServiceDescriptor.Lifetime"/> values are read - so it is
    /// AOT/trim-safe and never touches the dispatch hot path. A user re-registration of a handler (any
    /// lifetime, a different implementation, or even an identical re-<c>Add</c>) replaces the generator's
    /// descriptor and is left untouched, because <see cref="Apply"/> upgrades a handler only when the
    /// generator's own descriptor is still the winning (last) registration for its service type.
    /// </para>
    /// </summary>
    public static class HandlerLifetimeOptimizer
    {
        // Build-time staging keyed by the service collection instance. GC-scoped (no leak): the entry dies
        // with the collection, and Apply removes it once consumed. Startup-only - never on the hot path.
        private static readonly ConditionalWeakTable<IServiceCollection, List<StagedHandler>> Staged = new();

        private readonly struct StagedHandler
        {
            public StagedHandler(ServiceDescriptor descriptor, Type[] dependencies)
            {
                Descriptor = descriptor;
                Dependencies = dependencies;
            }

            public ServiceDescriptor Descriptor { get; }

            public Type[] Dependencies { get; }
        }

        /// <summary>
        /// Records, during handler registration, the generator's own handler descriptors (held by reference)
        /// and their constructor dependency types, to be resolved later by <see cref="Apply"/>. Called by
        /// generated code; safe to call multiple times (entries accumulate per collection).
        /// </summary>
        /// <param name="services">The service collection the handlers were added to.</param>
        /// <param name="handlers">For each optimizable handler: the exact <see cref="ServiceDescriptor"/> the
        /// generator added for it, and the constructor dependency types to inspect.</param>
        public static void Stage(
            IServiceCollection services,
            (ServiceDescriptor Descriptor, Type[] Dependencies)[] handlers)
        {
            ArgumentNullException.ThrowIfNull(services);
            if (handlers is null || handlers.Length == 0) return;

            var list = Staged.GetOrCreateValue(services);
            foreach (var h in handlers)
            {
                if (h.Descriptor is null || h.Dependencies is null) continue;
                list.Add(new StagedHandler(h.Descriptor, h.Dependencies));
            }
        }

        /// <summary>
        /// Resolves every staged handler's lifetime against the now-complete service collection and upgrades
        /// it in place where safe. Called by the generated finalization step before the pipeline chains are
        /// registered. Idempotent: consumes the staged entries, and a re-run finds nothing to do.
        /// </summary>
        /// <param name="services">The fully-populated service collection, immediately before building.</param>
        public static void Apply(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            if (!Staged.TryGetValue(services, out var staged) || staged.Count == 0) return;
            Staged.Remove(services);

            // Index the effective lifetime of every registered service type (last registration wins,
            // matching how GetRequiredService resolves). One pass, no reflection.
            var lifetimes = new Dictionary<Type, ServiceLifetime>(services.Count);
            foreach (var d in services)
                lifetimes[d.ServiceType] = d.Lifetime;

            foreach (var handler in staged)
            {
                var target = ComputeLifetime(lifetimes, handler.Dependencies);
                if (target == ServiceLifetime.Transient)
                    continue; // nothing to upgrade

                var descriptor = handler.Descriptor;

                // The last descriptor for the service type is the one DI resolves. Upgrade ours only when it
                // is STILL that winner (reference identity) - any user re-registration appends a newer
                // descriptor and is therefore respected, including an identical re-Add that forces Transient.
                int last = FindWinningDescriptorIndex(services, descriptor.ServiceType);

                if (last < 0 || !ReferenceEquals(services[last], descriptor))
                    continue;

                if (descriptor.ImplementationType is null)
                    continue; // generator always supplies an implementation type; guard defensively

                services[last] = new ServiceDescriptor(descriptor.ServiceType, descriptor.ImplementationType, target);
            }
        }

        /// <summary>
        /// Index of the last descriptor registered for <paramref name="serviceType"/> - the one DI resolves
        /// (last registration wins) - or -1 if none. A separate pass keeps <see cref="Apply"/> flat.
        /// </summary>
        private static int FindWinningDescriptorIndex(IServiceCollection services, Type serviceType)
        {
            for (int i = services.Count - 1; i >= 0; i--)
            {
                if (services[i].ServiceType == serviceType)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// The safe lifetime for a handler given its dependency lifetimes. Only called for handlers that
        /// HAVE dependencies (stateless handlers are already Singleton): Singleton if all are singletons,
        /// Scoped if any is scoped (and none transient), Transient if any is transient or unregistered.
        /// </summary>
        private static ServiceLifetime ComputeLifetime(Dictionary<Type, ServiceLifetime> lifetimes, Type[] deps)
        {
            var result = ServiceLifetime.Singleton;

            foreach (var dep in deps)
            {
                if (!lifetimes.TryGetValue(dep, out var lt))
                    return ServiceLifetime.Transient; // unknown dependency - stay safe

                if (lt == ServiceLifetime.Transient)
                    return ServiceLifetime.Transient; // a transient dependency keeps the handler transient

                if (lt == ServiceLifetime.Scoped)
                    result = ServiceLifetime.Scoped; // a scoped dependency caps the handler at Scoped
            }

            return result;
        }
    }
}
