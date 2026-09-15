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
    /// <b>Why it re-reads the collection.</b> The map captures the
    /// <see cref="IServiceCollection"/> at <c>AddMediator</c> time but does not read it until the
    /// first dispatch, which is necessarily after <c>BuildServiceProvider()</c>. By then the
    /// descriptor list is final, so a handler the user re-registered AFTER <c>AddMediator</c> — the
    /// documented way to override an auto-detected lifetime, last registration wins — is seen with
    /// the lifetime the provider actually honours. It also re-reads when the collection has grown since,
    /// so one collection built into two providers with registrations added in between describes each
    /// of them correctly rather than freezing the first one's answer for both.
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

            return serviceProvider.GetService<DispatchLifetimeSnapshot>()?.AllowsCaching(serviceType) == true;
        }

        /// <summary>
        /// Whether ANY registration for <paramref name="serviceType"/> is Transient.
        /// <para>
        /// <see cref="AllowsCaching"/> answers for the descriptor the container would RESOLVE — the last
        /// one — which is right for a service resolved singly, such as a handler or a chain. It is the
        /// wrong question for an ENUMERABLE service type: several descriptors coexist for
        /// <c>IPipelineBehavior&lt;,&gt;</c> and every one of them runs, so a later Scoped registration
        /// hid an earlier Transient one behind it and the generated validator went quiet about a behavior
        /// that really was being constructed once and shared.
        /// </para>
        /// <para>
        /// Used by validation only. Nothing on the dispatch path asks this: the caches key on service
        /// types that resolve singly, where last-wins is the correct reading.
        /// </para>
        /// <para>
        /// Fails CLOSED in the opposite direction to <see cref="AllowsCaching"/>: an unknown type answers
        /// <see langword="false"/>, so validation stays silent rather than inventing a fault.
        /// </para>
        /// </summary>
        public static bool AnyTransient(IServiceProvider serviceProvider, Type serviceType)
        {
            if (serviceProvider is null || serviceType is null)
                return false;

            return serviceProvider.GetService<DispatchLifetimeSnapshot>()?.AnyTransient(serviceType) == true;
        }
    }

    /// <summary>
    /// One container's answer, taken once and never revised.
    /// <para>
    /// Registered as a Singleton TYPE, which is what makes it per container: the DI container builds
    /// one per provider, and every scope of that provider resolves the same one. A scope and its root
    /// therefore always agree, and two providers built from one collection do not.
    /// </para>
    /// <para>
    /// That last part is the whole point. <see cref="DispatchLifetimeMap"/> is registered as an
    /// INSTANCE, so it is shared by every provider built from the same collection, and it holds one
    /// snapshot. Reading through it directly meant the second provider's rebuild overwrote the first
    /// provider's answer: measured, a provider whose handler really was Transient reported "fresh",
    /// then reported "cacheable" once a sibling provider with a Scoped registration had read the map —
    /// and would have pinned that Transient handler. Capturing the snapshot per provider ends that.
    /// </para>
    /// <para>
    /// Taken on first use, which is the first dispatch. Configuring a collection FURTHER after
    /// building a provider from it, and only then dispatching on that older provider, still reads the
    /// newer registrations — there is no hook at BuildServiceProvider to capture instead, and mutating
    /// a collection a provider was already built from is the ASP0000-shaped pattern this library
    /// documents for the aggressive tier.
    /// </para>
    /// </summary>
    internal sealed class DispatchLifetimeSnapshot(DispatchLifetimeMap map)
    {
        private readonly Dictionary<Type, DispatchLifetimeMap.Flags> _lifetimes = map.Current();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowsCaching(Type serviceType)
            => (Lookup(serviceType) & DispatchLifetimeMap.Flags.Cacheable) != 0;

        public bool AnyTransient(Type serviceType)
            => (Lookup(serviceType) & DispatchLifetimeMap.Flags.AnyTransient) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DispatchLifetimeMap.Flags Lookup(Type serviceType)
        {
            if (_lifetimes.TryGetValue(serviceType, out var flags))
                return flags;

            // A closed generic can be served by an open-generic registration, e.g.
            // AddTransient(typeof(IRequestHandler<,>), typeof(GenericHandler<,>)) — the closed type
            // is never a key. Fall back to the definition so those registrations are not forced
            // onto the uncached path.
            if (serviceType.IsConstructedGenericType
                && _lifetimes.TryGetValue(serviceType.GetGenericTypeDefinition(), out flags))
                return flags;

            return DispatchLifetimeMap.Flags.None;
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

        // STRONG, and it has to be. This was a WeakReference so the map would not pin the descriptor
        // list, and the snapshot is taken lazily on the first dispatch — so whenever a garbage
        // collection landed in between, which is most of the time given the collection is a local in
        // startup code, the target was gone and the map settled on an EMPTY snapshot. Empty answers
        // "not cacheable" for everything, so every provider-keyed cache in the library silently
        // stopped caching, permanently, and non-deterministically. Measured in a third-party
        // benchmark: a Scoped handler reported AllowsCaching=false while DI was handing back the same
        // instance. Retaining a List<ServiceDescriptor> for the life of the container is the price.
        private readonly IServiceCollection _services;

        /// <summary>
        /// The two readings of one descriptor list, kept together so a container pays for one map.
        /// They differ because the questions do: <see cref="Cacheable"/> is LAST-WINS, matching how the
        /// container resolves a single service, while <see cref="AnyTransient"/> is an OR, because an
        /// enumerable service type runs every descriptor registered for it.
        /// </summary>
        [Flags]
        internal enum Flags : byte
        {
            None = 0,
            Cacheable = 1,
            AnyTransient = 2,
        }

        private Dictionary<Type, Flags>? _snapshot;
        private int _snapshotCount = -1;

        public DispatchLifetimeMap(IServiceCollection services) => _services = services;

        /// <summary>
        /// The lifetimes as the collection reads right now, rebuilding first if it has changed.
        /// Called once per built provider, by <see cref="DispatchLifetimeSnapshot"/>'s constructor.
        /// </summary>
        public Dictionary<Type, Flags> Current()
        {
            var snapshot = Volatile.Read(ref _snapshot);
            return snapshot is null || IsStale() ? BuildSnapshot() : snapshot;
        }

        /// <summary>
        /// <see langword="true"/> when the collection has gained or lost registrations since the
        /// snapshot was taken.
        /// <para>
        /// Counting is enough. Descriptors are also REPLACED in place — <c>HandlerLifetimeOptimizer</c>
        /// raises a handler's lifetime that way — but that runs during registration, before any
        /// provider exists and therefore before any dispatch could have taken a snapshot. What a
        /// snapshot can miss is a container configured further and rebuilt, and that always changes the
        /// count.
        /// </para>
        /// </summary>
        private bool IsStale() => _services.Count != Volatile.Read(ref _snapshotCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Dictionary<Type, Flags> BuildSnapshot()
        {
            lock (_gate)
            {
                var existing = Volatile.Read(ref _snapshot);
                if (existing is not null && !IsStale())
                    return existing;

                var services = _services;
                var builder = new Dictionary<Type, Flags>(services.Count);

                foreach (var descriptor in services)
                {
                    builder.TryGetValue(descriptor.ServiceType, out var flags);
                    bool isTransient = descriptor.Lifetime == ServiceLifetime.Transient;

                    // Cacheable: last registration wins, matching how GetRequiredService resolves, so a
                    // later descriptor overwrites what an earlier one said.
                    flags = isTransient ? flags & ~Flags.Cacheable : flags | Flags.Cacheable;

                    // AnyTransient: an OR, because an enumerable service type runs EVERY descriptor
                    // registered for it — a later Scoped one does not undo an earlier Transient one.
                    if (isTransient)
                        flags |= Flags.AnyTransient;

                    builder[descriptor.ServiceType] = flags;
                }

                // A plain Dictionary, NOT a FrozenDictionary, and that is deliberate.
                //
                // This map is built once per collection and read only on a cache MISS — the hot path
                // never reaches it. FrozenDictionary buys fast reads with an expensive build, which
                // is the wrong side of that trade here, and the build lands on the first dispatch:
                // the single worst moment, because it is also the first JIT of the whole Frozen
                // construction path. Worse, Flags is a byte enum, so FrozenDictionary<Type, Flags>
                // shares no code with the FrozenDictionary<Type, DispatchDelegate> that
                // RequestObjectDispatch already built — it pays a SECOND, complete JIT of it.
                //
                // Measured on a genuinely cold process (one per sample): the mediator's share of
                // first dispatch fell 7.4 -> 3.4 ms, and 9.4 KB of JITted Frozen machinery — more
                // than all of this library's own dispatch code on that path — went away. The hot
                // path did not move (Send 3/5 behaviours: 5.55/6.54 -> 5.59/6.63 ns).
                //
                // Published only through Volatile.Write and never mutated afterwards, so concurrent
                // readers are as safe as they were with the frozen one.
                var snapshot = builder;

                // Count before snapshot: a reader that interleaves then sees a stale count against the
                // OLD snapshot and rebuilds once more, which is wasteful but never wrong. The reverse
                // order would let it accept the new snapshot under the old count.
                Volatile.Write(ref _snapshotCount, services.Count);
                Volatile.Write(ref _snapshot, snapshot);
                return snapshot;
            }
        }
    }
}
