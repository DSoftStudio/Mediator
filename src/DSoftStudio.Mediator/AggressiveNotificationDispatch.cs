// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator;

/// <summary>
/// Per-notification-type eligibility state for the ADR-0066 AGGRESSIVE Publish tier — the
/// notification mirror of <see cref="AggressiveDispatch{TRequest, TResponse}"/>.
/// <para>
/// The generated <c>NotificationRegistry.Register(IServiceCollection)</c> (called from
/// <c>PrecompileNotifications()</c>) calls <see cref="SetEligibility"/>. A notification type is
/// eligible when EVERY generated handler's LAST concrete self-registration descriptor is a
/// Singleton with a matching implementation type (the dispatch-table factories resolve by
/// CONCRETE type — see <c>DependencyInjectionGenerator</c>), AND no custom
/// <see cref="INotificationPublisher"/> is registered.
/// </para>
/// <para>
/// Arming is lazy, from the generated SAFE tier's resolve path on first publish:
/// <see cref="TryArm"/> RE-VERIFIES every handler descriptor and the publisher absence at arm
/// time. Armed holders are disarmed by the process latch (second container) AND by
/// <see cref="NotificationPublisherFlag"/> when a custom publisher appears at runtime.
/// One attempt per process per type; failure is permanent (fail-safe).
/// </para>
/// This is generator infrastructure — not intended for user code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AggressiveNotificationDispatch<TNotification>
    where TNotification : INotification
{
    private const int Ineligible = 0, Eligible = 1, Attempted = 2;

    private static int _state;
    private static IServiceCollection? _services;
    private static Type[]? _handlerTypes;

    /// <summary>
    /// Records this notification type's eligibility. <paramref name="handlerTypes"/> is the
    /// generated handler set in dispatch order; eligibility requires every handler's LAST
    /// concrete descriptor to be Singleton self-registered, and no
    /// <see cref="INotificationPublisher"/> descriptor in <paramref name="services"/>.
    /// </summary>
    public static void SetEligibility(IServiceCollection services, Type[] handlerTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(handlerTypes);

        bool eligible = handlerTypes.Length > 0
            && !AggressiveDispatchLatch.IsPoisoned
            && !NotificationPublisherFlag.HasCustomPublisher
            && VerifyDescriptors(services, handlerTypes);

        if (!eligible)
        {
            // Never regress Attempted -> Ineligible: one-shot semantics stay one-shot.
            Interlocked.CompareExchange(ref _state, Ineligible, Eligible);
            _services = null;
            _handlerTypes = null;
            return;
        }

        _services = services;
        _handlerTypes = handlerTypes;
        Interlocked.CompareExchange(ref _state, Eligible, Ineligible);
    }

    /// <summary>Fast pre-check read by the generated SAFE-tier resolve path (miss path only).</summary>
    public static bool ShouldAttemptArm => Volatile.Read(ref _state) == Eligible;

    /// <summary>
    /// One-shot arm attempt. Verifies, at arm time, that every resolved instance's exact type
    /// matches the generated handler set AND is still the winning Singleton self-registration,
    /// and that no custom publisher exists. On success, <paramref name="arm"/> runs under the
    /// process latch and <paramref name="disarm"/> is registered with BOTH the latch (container
    /// poison) and the publisher hook (late custom-publisher registration).
    /// </summary>
    public static bool TryArm(object[] handlerInstances, Action arm, Action disarm)
    {
        ArgumentNullException.ThrowIfNull(handlerInstances);

        if (Interlocked.CompareExchange(ref _state, Attempted, Eligible) != Eligible)
            return false;

        var services = _services;
        var handlerTypes = _handlerTypes;
        _services = null; // release the collection graph either way
        _handlerTypes = null;

        if (services is null
            || handlerTypes is null
            || handlerInstances.Length != handlerTypes.Length
            || NotificationPublisherFlag.HasCustomPublisher)
        {
            return false;
        }

        for (int i = 0; i < handlerInstances.Length; i++)
        {
            if (handlerInstances[i].GetType() != handlerTypes[i])
                return false;
        }

        if (!VerifyDescriptors(services, handlerTypes))
            return false;

        if (!AggressiveDispatchLatch.TryArmNotification(arm, disarm))
            return false;

        // Outside the latch lock (see AggressiveDispatch<,>.TryArm for the full rationale):
        // best-effort transition record; the aggressive-armed gauge is authoritative.
        if (!AggressiveDispatchLatch.IsPoisoned)
            AggressiveDispatchEventSource.Log.AggressiveArmed(typeof(TNotification).Name);
        return true;
    }

    /// <summary>
    /// LAST-wins verification of the concrete self-registrations the dispatch-table factories
    /// resolve, plus the publisher-absence check (a registered <see cref="INotificationPublisher"/>
    /// changes Publish semantics for every notification type).
    /// </summary>
    private static bool VerifyDescriptors(IServiceCollection services, Type[] handlerTypes)
    {
        var winners = new ServiceDescriptor?[handlerTypes.Length];

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(INotificationPublisher))
                return false;

            for (int i = 0; i < handlerTypes.Length; i++)
            {
                if (descriptor.ServiceType == handlerTypes[i])
                    winners[i] = descriptor; // keep LAST — MSDI resolution semantics
            }
        }

        for (int i = 0; i < handlerTypes.Length; i++)
        {
            var winner = winners[i];
            if (winner is null
                || winner.Lifetime != ServiceLifetime.Singleton
                || (winner.ImplementationType
                    ?? winner.ImplementationInstance?.GetType()) != handlerTypes[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Test-only companion to <see cref="AggressiveDispatchLatch.ResetForTests"/>.</summary>
    internal static void ResetForTests()
    {
        _state = Ineligible;
        _services = null;
        _handlerTypes = null;
    }
}
