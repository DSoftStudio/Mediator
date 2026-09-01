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
    /// <summary>
    /// Opt-in fail-closed mode for CI: when this <see cref="AppContext"/> switch is enabled, a
    /// poison that disarms at least one ARMED holder throws <see cref="InvalidOperationException"/>
    /// (from the second container's <c>AddMediator</c> call — catchable and deterministic)
    /// instead of degrading silently. Never triggered by test resets.
    /// </summary>
    internal const string StrictPoisonSwitchName = "DSoftStudio.Mediator.AggressiveDispatch.StrictPoison";

    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<IServiceCollection, object> SeenCollections = new();
    private static int _containerCount;
    private static int _poisoned;
    private static Action? _disarmCallbacks;
    private static int _armedHolderCount;

    // ADR-0066: notification (Publish) holders form a SECOND disarm group. A container poison
    // disarms both groups; a custom INotificationPublisher appearing at runtime disarms ONLY
    // this group (Send holders are unaffected — the publisher changes Publish semantics only).
    private static Action? _notifDisarmCallbacks;
    private static int _armedNotifHolderCount;

    // Backing values for the aggressive-armed / aggressive-poisoned PollingCounters. They live
    // HERE, not on the EventSource: touching a static field of the EventSource class would run
    // its type initializer, whose construction synchronously notifies every in-proc
    // EventListener.OnEventSourceCreated — arbitrary third-party code that must never execute
    // under <see cref="Gate"/>. ArmedCount is a CURRENT-STATE gauge (mirrors
    // <see cref="_armedHolderCount"/>: decremented on poison, zeroed on reset);
    // PoisonedCount is cumulative.
    internal static int ArmedCount;
    internal static int PoisonedCount;

    /// <summary>True once a second container has been observed — one-way, process-wide.</summary>
    public static bool IsPoisoned => Volatile.Read(ref _poisoned) != 0;

    /// <summary>
    /// Reports a container registration. Called by core <c>AddMediator</c> for every distinct
    /// <see cref="IServiceCollection"/> (repeat calls on the same collection are idempotent).
    /// </summary>
    public static void OnContainerRegistered(IServiceCollection services)
    {
        int disarmed;
        lock (Gate)
        {
            if (SeenCollections.TryGetValue(services, out _))
                return;
            SeenCollections.Add(services, Sentinel);

            if (++_containerCount < 2 || _poisoned != 0)
                return;

            disarmed = PoisonLocked();
        }

        // The EventSource write and the opt-in strict throw both run OUTSIDE the lock:
        // WriteEvent synchronously invokes in-proc EventListener callbacks (OTel bridges, APM
        // agents), and third-party code must never run under the lock that serializes every
        // AddMediator and every first-dispatch arm. State is already safe here — everything
        // degraded to SAFE before the lock was released.
        AggressiveDispatchEventSource.Log.AggressivePoisoned("second-container", disarmed);

        if (disarmed > 0
            && AppContext.TryGetSwitch(StrictPoisonSwitchName, out var strict)
            && strict)
        {
            throw new InvalidOperationException(
                "DSoftStudio.Mediator: a second IServiceCollection was registered while the " +
                "AGGRESSIVE fast path was armed; the tier has degraded to SAFE. This throw is " +
                $"opt-in via AppContext switch '{StrictPoisonSwitchName}' (fail-closed CI mode). " +
                "Either this process legitimately builds multiple containers (disable the switch, " +
                "or set the DSoftMediatorDisableAggressive MSBuild property) or a container is " +
                "being created unexpectedly.");
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
            _armedHolderCount++;
            Interlocked.Increment(ref ArmedCount);
            return true;
        }
    }

    /// <summary>
    /// ADR-0066: arms a NOTIFICATION holder (second disarm group). Same latch semantics as
    /// <see cref="TryArm"/>, but the holder is additionally disarmed when a custom
    /// <see cref="Abstractions.INotificationPublisher"/> appears at runtime
    /// (via <see cref="DisarmNotifications"/>).
    /// </summary>
    internal static bool TryArmNotification(Action arm, Action disarm)
    {
        lock (Gate)
        {
            if (_poisoned != 0)
                return false;

            arm();
            _notifDisarmCallbacks += disarm;
            _armedNotifHolderCount++;
            Interlocked.Increment(ref ArmedCount);
            return true;
        }
    }

    /// <summary>
    /// Disarms ONLY the notification group (Send holders stay armed). Fired when a custom
    /// publisher registers after arming. Returns the number of holders disarmed; the CALLER
    /// fires the EventSource write outside the lock.
    /// </summary>
    internal static int DisarmNotifications()
    {
        lock (Gate)
        {
            var callbacks = _notifDisarmCallbacks;
            var disarmed = _armedNotifHolderCount;
            _notifDisarmCallbacks = null;
            _armedNotifHolderCount = 0;
            Interlocked.Add(ref ArmedCount, -disarmed);
            callbacks?.Invoke();
            return disarmed;
        }
    }

    /// <summary>
    /// Poisons the tier and disarms every holder, all under <see cref="Gate"/> (the disarm
    /// callbacks are trivial generated <c>Volatile.Write(ref _armed, null)</c> closures — no
    /// user or listener code). Returns the number of holders disarmed; the caller fires the
    /// poison event and the strict throw AFTER releasing the lock.
    /// </summary>
    private static int PoisonLocked()
    {
        Volatile.Write(ref _poisoned, 1);
        var callbacks = _disarmCallbacks;
        var disarmed = _armedHolderCount + _armedNotifHolderCount;
        _disarmCallbacks = null;
        _armedHolderCount = 0;
        var notifCallbacks = _notifDisarmCallbacks;
        _notifDisarmCallbacks = null;
        _armedNotifHolderCount = 0;
        Interlocked.Add(ref ArmedCount, -disarmed);
        Interlocked.Increment(ref PoisonedCount);
        callbacks?.Invoke();
        notifCallbacks?.Invoke();
        return disarmed;
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
            _armedHolderCount = 0;
            _armedNotifHolderCount = 0;
            Interlocked.Exchange(ref ArmedCount, 0); // gauge mirrors the armed-holder counts
            var callbacks = _disarmCallbacks;
            _disarmCallbacks = null;
            var notifCallbacks = _notifDisarmCallbacks;
            _notifDisarmCallbacks = null;
            callbacks?.Invoke();
            notifCallbacks?.Invoke();
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

        if (!AggressiveDispatchLatch.TryArm(arm, disarm))
            return false;

        // Fired OUTSIDE the latch lock (in-proc EventListener callbacks must never run under
        // it), so a concurrent poison can land in between — re-check and skip rather than
        // record an arm AFTER the poison that already disarmed this very holder. A poison can
        // still slip inside the few instructions between this check and the write; events are
        // best-effort transition records — the aggressive-armed gauge (maintained under the
        // lock) is the authoritative current-state signal.
        if (!AggressiveDispatchLatch.IsPoisoned)
            AggressiveDispatchEventSource.Log.AggressiveArmed(typeof(TRequest).Name);
        return true;
    }

    /// <summary>Test-only companion to <see cref="AggressiveDispatchLatch.ResetForTests"/>.</summary>
    internal static void ResetForTests()
    {
        _state = Ineligible;
        _services = null;
    }
}
