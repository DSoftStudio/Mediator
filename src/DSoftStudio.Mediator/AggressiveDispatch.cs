// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator;

/// <summary>
/// Process-wide latch for the ADR-0065 AGGRESSIVE dispatch tier (default-ON, self-gating).
/// <para>
/// The generated fast path caches a Singleton handler instance in a static holder and calls it
/// directly — correct ONLY while a single root container exists. This latch is the safety net:
/// core <see cref="ServiceCollectionExtensions.AddMediator(IServiceCollection)"/> reports every
/// distinct <see cref="IServiceCollection"/> here, and the SECOND one permanently poisons the
/// tier — every armed holder is disarmed (via callbacks registered at arm time) and no holder
/// ever arms again. Dispatch degrades to the SAFE tier; it never returns another container's
/// handler for dispatches that begin after the second container registers. In-flight dispatches
/// that already read a non-null holder belong to container #1, whose Singleton is the correct
/// answer for them (poisoning happens during container #2's REGISTRATION, strictly before it can
/// build a provider, let alone dispatch).
/// </para>
/// <para>
/// Known boundary (documented in ADR-0065 Amendment §A5/§A6): ONE collection built into TWO
/// providers (<c>BuildServiceProvider()</c> called twice) is indistinguishable here — provider #2
/// receives provider #1's Singleton on armed types. That pattern is an ASP0000-class smell;
/// set the <c>DSoftMediatorDisableAggressive</c> MSBuild property to force the tier off.
/// </para>
/// This is generator infrastructure — not intended for user code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AggressiveDispatchLatch
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<IServiceCollection, object> SeenCollections = new();
    private static int _containerCount;
    private static int _poisoned;
    private static Action? _disarmCallbacks;

    /// <summary>True once a second container has been observed — one-way, process-wide.</summary>
    public static bool IsPoisoned => Volatile.Read(ref _poisoned) != 0;

    /// <summary>
    /// Reports a container registration. Called by core <c>AddMediator</c> for every distinct
    /// <see cref="IServiceCollection"/> (repeat calls on the same collection are idempotent).
    /// </summary>
    public static void OnContainerRegistered(IServiceCollection services)
    {
        lock (Gate)
        {
            if (SeenCollections.TryGetValue(services, out _))
                return;
            SeenCollections.Add(services, Sentinel);

            if (++_containerCount >= 2)
                PoisonLocked();
        }
    }

    private static readonly object Sentinel = new();

    /// <summary>
    /// Arms a holder under the latch lock: <paramref name="arm"/> runs only while the tier is
    /// not poisoned, and <paramref name="disarm"/> is registered so a later poison clears the
    /// holder. Serialized against <see cref="OnContainerRegistered"/> — a poison never races an
    /// arm. Returns false (without invoking <paramref name="arm"/>) when already poisoned.
    /// </summary>
    public static bool TryArm(Action arm, Action disarm)
    {
        lock (Gate)
        {
            if (_poisoned != 0)
                return false;

            arm();
            _disarmCallbacks += disarm;
            return true;
        }
    }

    private static void PoisonLocked()
    {
        if (_poisoned != 0)
            return;

        Volatile.Write(ref _poisoned, 1);
        var callbacks = _disarmCallbacks;
        _disarmCallbacks = null;
        callbacks?.Invoke();
    }

    /// <summary>
    /// Test-only: restores the pristine single-container state. The latch is process-global and
    /// one-way by design; test suites that build many containers need isolation between cases.
    /// </summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _containerCount = 0;
            _poisoned = 0;
            var callbacks = _disarmCallbacks;
            _disarmCallbacks = null;
            callbacks?.Invoke();
            SeenCollections.Clear();
        }
    }
}

/// <summary>
/// Per-request-type eligibility state for the ADR-0065 AGGRESSIVE dispatch tier.
/// <para>
/// The generated <c>RegisterPipeline&lt;TRequest, TResponse&gt;</c> scan (which runs from
/// <c>PrecompilePipelines()</c>/<c>AddMediator(configure)</c>, AFTER
/// <see cref="HandlerLifetimeOptimizer.Apply"/>, so lifetimes are final) calls
/// <see cref="SetEligibility"/>. A type is eligible when it has no pipeline chain and its
/// LAST-registered <see cref="IRequestHandler{TRequest, TResponse}"/> descriptor is a Singleton
/// with a concrete <c>ImplementationType</c> (MSDI last-wins semantics).
/// </para>
/// <para>
/// Arming is lazy, from the generated SlowPath on first dispatch: <see cref="TryArm"/>
/// RE-VERIFIES the winning descriptor against the captured collection at arm time — a
/// post-precompile re-registration (same concrete type, narrower lifetime, or a different
/// implementation) makes arming fail and the type stays on the SAFE tier. One attempt per
/// process per type; failure is permanent (fail-safe, zero further overhead).
/// </para>
/// This is generator infrastructure — not intended for user code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AggressiveDispatch<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int Ineligible = 0, Eligible = 1, Attempted = 2;

    private static int _state;
    private static IServiceCollection? _services;

    /// <summary>
    /// Records this type's eligibility, computed by the generated registration scan.
    /// A later scan (same or another container) overwrites — but once the process latch is
    /// poisoned, or an arm attempt was made, eligibility can no longer be granted.
    /// </summary>
    public static void SetEligibility(IServiceCollection services, bool eligible)
    {
        if (!eligible || AggressiveDispatchLatch.IsPoisoned)
        {
            // Never regress Attempted -> Ineligible: the one-shot semantics stay one-shot.
            Interlocked.CompareExchange(ref _state, Ineligible, Eligible);
            _services = null;
            return;
        }

        _services = services;
        Interlocked.CompareExchange(ref _state, Eligible, Ineligible);
    }

    /// <summary>Fast pre-check read by the generated SlowPath (miss path only).</summary>
    public static bool ShouldAttemptArm => Volatile.Read(ref _state) == Eligible;

    /// <summary>
    /// One-shot arm attempt. Verifies, at arm time, that the resolved instance's type IS the
    /// winning registration: the LAST <see cref="IRequestHandler{TRequest, TResponse}"/>
    /// descriptor in the captured collection must be Singleton with
    /// <c>ImplementationType == handlerInstance.GetType()</c>. On success, <paramref name="arm"/>
    /// runs under the process latch (poison-safe) and <paramref name="disarm"/> is registered.
    /// </summary>
    public static bool TryArm(object handlerInstance, Action arm, Action disarm)
    {
        if (Interlocked.CompareExchange(ref _state, Attempted, Eligible) != Eligible)
            return false;

        var services = _services;
        _services = null; // release the collection graph either way

        if (services is null)
            return false;

        ServiceDescriptor? winner = null;
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IRequestHandler<TRequest, TResponse>))
                winner = descriptor; // keep LAST — MSDI resolution semantics
        }

        if (winner is null
            || winner.Lifetime != ServiceLifetime.Singleton
            || (winner.ImplementationType ?? winner.ImplementationInstance?.GetType()) != handlerInstance.GetType())
        {
            return false;
        }

        return AggressiveDispatchLatch.TryArm(arm, disarm);
    }

    /// <summary>Test-only companion to <see cref="AggressiveDispatchLatch.ResetForTests"/>.</summary>
    internal static void ResetForTests()
    {
        _state = Ineligible;
        _services = null;
    }
}
