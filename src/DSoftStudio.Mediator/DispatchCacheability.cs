// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Answers one question for a single container: may an instance of this service type be reused
    /// across dispatches on the same <see cref="IServiceProvider"/>?
    /// <para>
    /// It may when the registration is Singleton or Scoped — a scope IS a provider, so the
    /// provider-keyed <c>[ThreadStatic]</c> caches key on exactly the right thing. It may NOT when
    /// the registration is Transient: the container promises a fresh instance per resolve, and the
    /// caches were quietly breaking that promise.
    /// </para>
    /// <para>
    /// <b>Why a per-container map and not a static flag.</b> The dispatch flags on
    /// <see cref="RequestDispatch{TRequest, TResponse}"/> are one static per closed generic pair,
    /// so they are PROCESS-global and monotonic, while a lifetime is decided per container. Two
    /// containers in one process disagree all the time — a test suite, a modular monolith, a host
    /// that rebuilds its provider — and the first one to register a cacheable chain latched the
    /// flag on for every container that followed. This map is registered per container by
    /// <see cref="ServiceCollectionExtensions.AddMediator(IServiceCollection)"/>, so each container
    /// answers for itself.
    /// </para>
    /// <para>
    /// <b>Why it reads the collection lazily.</b> The map captures the
    /// <see cref="IServiceCollection"/> at <c>AddMediator</c> time but does not read it until the
    /// first dispatch, which is necessarily after <c>BuildServiceProvider()</c>. By then the
    /// descriptor list is final, so a handler the user re-registered AFTER <c>AddMediator</c> — the
    /// documented way to override an auto-detected lifetime, last registration wins — is seen with
    /// the lifetime the provider actually honours.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DispatchCacheability
    {
        /// <summary>
        /// <see langword="true"/> when an instance of <paramref name="serviceType"/> resolved from
        /// <paramref name="serviceProvider"/> may be cached per (thread, provider).
        /// <para>
        /// Callers must consult this on the COLD miss path only, and remember the answer alongside
        /// the cached provider — it costs a singleton resolve plus a dictionary probe, which is
        /// nothing next to the resolve it guards but is not hot-path work.
        /// </para>
        /// <para>
        /// Fails CLOSED: an unknown service type, or a provider with no map (a container that never
        /// went through <c>AddMediator</c>), answers <see langword="false"/>. Refusing to cache is
        /// always correct, merely slower; caching wrongly is a captive dependency.
        /// </para>
        /// </summary>
        public static bool AllowsCaching(IServiceProvider serviceProvider, Type serviceType)
        {
            if (serviceProvider is null || serviceType is null)
                return false;

            return serviceProvider.GetService<DispatchLifetimeMap>()?.AllowsCaching(serviceType) == true;
        }
    }

    /// <summary>
    /// The per-container lifetime snapshot behind <see cref="DispatchCacheability"/>. Registered as
    /// a singleton instance by <c>AddMediator</c>; internal because nothing outside this assembly
    /// resolves it — generated code goes through <see cref="DispatchCacheability"/>.
    /// </summary>
    internal sealed class DispatchLifetimeMap
    {
        private readonly object _gate = new();

        // Held only until the first read, then dropped so the map does not pin the descriptor list
        // (and everything the descriptors' implementation instances reference) for the life of the
        // provider.
        private IServiceCollection? _services;
        private FrozenDictionary<Type, bool>? _snapshot;

        public DispatchLifetimeMap(IServiceCollection services) => _services = services;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowsCaching(Type serviceType)
        {
            var snapshot = Volatile.Read(ref _snapshot) ?? BuildSnapshot();

            if (snapshot.TryGetValue(serviceType, out bool cacheable))
                return cacheable;

            // A closed generic can be served by an open-generic registration, e.g.
            // AddTransient(typeof(IRequestHandler<,>), typeof(GenericHandler<,>)) — the closed type
            // is never a key. Fall back to the definition so those registrations are not forced
            // onto the uncached path.
            if (serviceType.IsConstructedGenericType
                && snapshot.TryGetValue(serviceType.GetGenericTypeDefinition(), out cacheable))
                return cacheable;

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private FrozenDictionary<Type, bool> BuildSnapshot()
        {
            lock (_gate)
            {
                var existing = Volatile.Read(ref _snapshot);
                if (existing is not null)
                    return existing;

                var services = _services;
                var builder = new Dictionary<Type, bool>(services?.Count ?? 0);

                if (services is not null)
                {
                    // Last registration wins, matching how GetRequiredService resolves — so assign
                    // unconditionally and let later descriptors overwrite earlier ones.
                    foreach (var descriptor in services)
                        builder[descriptor.ServiceType] = descriptor.Lifetime != ServiceLifetime.Transient;
                }

                var snapshot = builder.ToFrozenDictionary();
                Volatile.Write(ref _snapshot, snapshot);
                _services = null;
                return snapshot;
            }
        }
    }
}
