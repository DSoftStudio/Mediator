// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Tracing;

namespace DSoftStudio.Mediator;

/// <summary>
/// Observability for the ADR-0065 AGGRESSIVE dispatch tier. All instrumentation hangs off COLD
/// latch transitions (arm / poison — at most a handful per process lifetime); the dispatch hot
/// path is never touched.
/// <para>
/// The provider name is FIXED (matching the existing <c>DSoftStudio-Mediator-*</c> convention)
/// so <c>dotnet-counters monitor --counters DSoftStudio-Mediator-FastPath</c> and any
/// EventSource/OTel bridge can key on it without per-app configuration. Duplicate-named
/// EventSources across assemblies do not throw; this one is process-singleton anyway (it ships
/// in the runtime package, next to the latch).
/// </para>
/// <list type="bullet">
///   <item><c>aggressive-armed</c> counter — CURRENT-STATE gauge of armed fast paths (drops to
///     zero when the tier poisons). AUTHORITATIVE: maintained under the latch lock.</item>
///   <item><c>aggressive-poisoned</c> counter — cumulative tier poisons (0 or 1 outside tests).</item>
///   <item><c>AggressiveArmed(requestType)</c> / <c>AggressivePoisoned(reason, disarmedCount)</c>
///     events — best-effort transition records, written OUTSIDE the latch lock so in-proc
///     EventListener callbacks never execute under it. Cross-thread event ORDER is therefore
///     not guaranteed; reconstruct state from the counters, not the event sequence.</item>
/// </list>
/// The counter backing fields live on <see cref="AggressiveDispatchLatch"/> — see the note
/// there on why the latch must never touch this type while holding its lock.
/// </summary>
internal sealed class AggressiveDispatchEventSource : EventSource
{
    internal const string SourceName = "DSoftStudio-Mediator-FastPath";

    internal static readonly AggressiveDispatchEventSource Log = new();

#pragma warning disable IDE0052 // counters are rooted by the EventSource, read by dotnet-counters
    private PollingCounter? _armedCounter;
    private PollingCounter? _poisonedCounter;
#pragma warning restore IDE0052

    private AggressiveDispatchEventSource()
        : base(SourceName)
    {
    }

    [Event(1, Level = EventLevel.Informational)]
    internal void AggressiveArmed(string requestType)
    {
        if (IsEnabled())
            WriteEvent(1, requestType);
    }

    [Event(2, Level = EventLevel.Warning)]
    internal void AggressivePoisoned(string reason, int disarmedCount)
    {
        if (IsEnabled())
            WriteEvent(2, reason, disarmedCount);
    }

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command != EventCommand.Enable)
            return;

        // Lazily created on first enable; guarded so an exotic ETW/EventPipe failure can never
        // take down the app (observability must stay strictly optional).
        try
        {
            _armedCounter ??= new PollingCounter(
                "aggressive-armed", this,
                static () => Volatile.Read(ref AggressiveDispatchLatch.ArmedCount))
            {
                DisplayName = "AGGRESSIVE fast paths armed",
            };
            _poisonedCounter ??= new PollingCounter(
                "aggressive-poisoned", this,
                static () => Volatile.Read(ref AggressiveDispatchLatch.PoisonedCount))
            {
                DisplayName = "AGGRESSIVE tier poisons",
            };
        }
        catch
        {
            // Counters unavailable — events still work.
        }
    }
}
