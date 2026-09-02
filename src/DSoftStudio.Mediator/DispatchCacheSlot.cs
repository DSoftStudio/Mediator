// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// The (provider, value) pair a provider-keyed dispatch cache holds for one thread.
    /// <para>
    /// The caches used to keep these as two separate <c>[ThreadStatic]</c> fields. Two problems with
    /// that, and this type fixes both. The hot path paid TWO thread-local lookups where one now
    /// suffices — the pair lives in one object, and the fields inside it are ordinary reads. And a
    /// <c>[ThreadStatic]</c> can only be written by its own thread, so nothing could ever release a
    /// slot on behalf of the thread that filled it; a slot object CAN be cleared by whoever disposes
    /// the scope, which is what <see cref="DispatchCacheReleaser"/> does.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DispatchCacheSlot<TValue> : IDispatchCacheSlot
        where TValue : class
    {
        /// <summary>
        /// The provider this slot's contents belong to, or <see langword="null"/> when the slot is
        /// empty. Reference assignment is atomic, so a release from another thread is safe: the owning
        /// thread either sees the old provider — and only matches it if the caller passed that same,
        /// now-disposed provider, which no live dispatch does — or sees null and re-resolves.
        /// </summary>
        public IServiceProvider? Provider;

        /// <summary>
        /// The cached instance, or <see langword="null"/> when this provider registered the service
        /// Transient. A non-null <see cref="Provider"/> with a null value is the "known not reusable
        /// here" state: resolve fresh each time.
        /// </summary>
        public TValue? Value;

        /// <summary>
        /// <see langword="true"/> when <see cref="Provider"/> has NO such service at all — as opposed
        /// to having registered it Transient. Both leave <see cref="Value"/> null, and conflating them
        /// is expensive: an absent service was re-resolved from the container on EVERY dispatch,
        /// forever, to be told null again. Only the optional caches (the pipeline chains) set this;
        /// for a handler, absence is an error rather than a state.
        /// </summary>
        public bool Absent;

        /// <inheritdoc />
        public void ReleaseIf(IServiceProvider provider)
        {
            if (!ReferenceEquals(Provider, provider))
                return;

            // Order matters: drop the contents first, then the key. A thread reading between the
            // writes sees a matching provider with a null value, which is the "resolve fresh" state —
            // correct, just slower for one dispatch.
            //
            // Absent MUST be cleared here. A released slot is reused by whichever provider next
            // occupies it, and one that still claimed "absent" would report "no chain" for a
            // container that has one — a behavior silently skipped, which is the exact class of bug
            // this slot indirection exists to prevent.
            Value = null;
            Absent = false;
            Provider = null;
        }
    }

    /// <summary>Non-generic handle so <see cref="DispatchCacheReleaser"/> can hold slots of every
    /// closed generic type in one list.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDispatchCacheSlot
    {
        /// <summary>Empties this slot if it currently holds <paramref name="provider"/>.</summary>
        void ReleaseIf(IServiceProvider provider);
    }

    /// <summary>
    /// Releases the dispatch caches' slots for a provider when that provider's scope is disposed.
    /// <para>
    /// Without this, a slot keeps a disposed scope reachable — and through it every scoped instance
    /// the scope resolved — until the thread that filled the slot happens to dispatch the same
    /// request type again against a different provider. For a request type served rarely, or a pool
    /// thread that moves on to other work, that is never. Measured: a scoped payload survives
    /// <c>scope.Dispose()</c> and a full <c>GC.Collect</c>, and becomes collectible only once the
    /// slot is overwritten — the slot is the sole root.
    /// </para>
    /// <para>
    /// Slots are tracked per provider in a <see cref="ConditionalWeakTable{TKey, TValue}"/>, whose
    /// weak keys mean the table never becomes the thing that keeps a provider alive. Registration
    /// happens on the caches' cold miss path, once per (thread, service, provider).
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DispatchCacheReleaser
    {
        private static readonly ConditionalWeakTable<IServiceProvider, SlotList> Tracked = new();

        /// <summary>
        /// Records that <paramref name="slot"/> now holds something belonging to
        /// <paramref name="provider"/>, so disposing that provider's scope can empty it. Cold path
        /// only — callers must not call this on a cache hit.
        /// </summary>
        public static void Track(IServiceProvider provider, IDispatchCacheSlot slot)
        {
            if (provider is null || slot is null)
                return;

            Tracked.GetOrCreateValue(provider).Add(slot);
        }

        /// <summary>
        /// Empties every tracked slot still holding <paramref name="provider"/>. Called when the
        /// scope is disposed, from whatever thread disposes it — which is the point of the slot
        /// indirection, since that is rarely the thread that filled the slot.
        /// </summary>
        public static void ReleaseFor(IServiceProvider provider)
        {
            if (provider is null || !Tracked.TryGetValue(provider, out var slots))
                return;

            slots.ReleaseAll(provider);
            Tracked.Remove(provider);
        }

        private sealed class SlotList
        {
            private readonly List<IDispatchCacheSlot> _slots = [];

            public void Add(IDispatchCacheSlot slot)
            {
                lock (_slots)
                {
                    // A slot is added once per (thread, service, provider): the cache only reaches
                    // here on a miss, and the very next dispatch on this thread finds the provider
                    // already in the slot. The scan stays short, and skipping the duplicate keeps it
                    // that way when a thread re-fills a slot it had released.
                    for (int i = 0; i < _slots.Count; i++)
                    {
                        if (ReferenceEquals(_slots[i], slot))
                            return;
                    }

                    _slots.Add(slot);
                }
            }

            public void ReleaseAll(IServiceProvider provider)
            {
                lock (_slots)
                {
                    for (int i = 0; i < _slots.Count; i++)
                        _slots[i].ReleaseIf(provider);

                    _slots.Clear();
                }
            }
        }
    }
}
