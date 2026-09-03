---
layout: default
title: "ADR-0007: Notification Observation Port - DSoftStudio.Mediator"
description: "A read-only port for observing notification dispatch, so instrumentation stops having to replace the publisher."
---

# ADR-0007: Notification Observation Port

**Status:** Accepted (revision 3 — measured by a vertical slice on `spike/notification-observation-port`.
Revision 1 was reviewed from five angles and rejected; what that review measured is recorded below
rather than quietly dropped)
**Date:** 2026-09-03
**Affects:** `DSoftStudio.Mediator.Abstractions`, the notification dispatch paths, `DSoftStudio.Mediator.OpenTelemetry`

---

## Context

The core has one observation port, `IMediatorDispatchObserver`, and it is request-only by generic
constraint. Notifications have none, so the OpenTelemetry bridge instruments publishes by
**replacing** the registered `INotificationPublisher` — and installs one even when the application
registered none.

That is not observation. Registering any `INotificationPublisher` disarms the generated notification
fast path, forces a `GetServices` per publish, changes **which** handlers run (a handler registered by
hand against the interface is skipped by default and invoked once a publisher exists), and yields a
**different singleton instance** for a handler that keeps state.

Simply not installing one is blocked: `InstrumentedNotificationPublisher` is the only producer of
notification spans, and the downstream tooling keys its imported-trace flame graph on them.

---

## What the review of the first draft established

The first draft proposed `BeginHandler(object) -> IDisposable` and a budget of "one null test per
publish, the same as the request side". Five independent reviews, all measuring rather than arguing,
found the following. These are recorded because each one constrains the design.

**The request side is not the clean precedent the draft claimed.** Registering an *inactive*
`IMediatorDispatchObserver` makes the generator force a pipeline chain for every request type, which
nulls `_fastPath` (`PipelineChainHandler.cs:104` reads `_observer is null &&`). Measured: handler-only
`Send` **6.66 → 11.47 ns, +72%**. The existing port already perturbs what it observes.

**The problem is the missing async frame, not the signature.** Disposal does *not* force the
synchronous path to await — an inline `Dispose()` on the `IsCompletedSuccessfully` branch measured
2.41 ns against a 2.20 ns control. But four different API shapes were measured and three leak, because
every notification entry point is a **synchronous** `Task`-returning method. Today's wrapper is immune
only because `InstrumentedHandler.Handle` is `async Task`, and `AsyncMethodBuilderCore.Start` restores
the caller's `ExecutionContext` at the synchronous-return boundary.

**A handler that throws synchronously escapes entirely.** Measured: zero spans stopped — both the
subscriber span and the envelope leak unexported, and `Activity.Current` is left on a never-stopped
Activity. The generated bodies have no `try`. Since exporters fire on `ActivityStopped`, the whole
publish disappears.

**Explicit parent context silently drops Baggage.** Every one of the four candidate designs parented
the subscriber with an explicit `ActivityContext`. Measured: `explicit-parent child sees tenant =
<null>` against `ambient-parent child sees tenant = acme`. Every outgoing `baggage` header and every
log enrichment that reads `Activity.Current.Baggage` inside a subscriber dies. No design listed this
as a risk. **Anchoring ambiently — set `Activity.Current` to the envelope, then `StartActivity` —
satisfies star parenting, non-chaining, nested handler spans, baggage on both the subscriber and its
own DB spans, whole-Task duration and an intact caller ambient, and holds under the parallel publisher
with three concurrent subscribers.**

**Two claims in the first draft were false**, and are corrected here: an Activity started from an
`ActivityContext` does *not* leave `Current` null on `Stop()` (.NET tracks the previous ambient, not
`Parent`), and ambient parenting is *not* wrong for the second handler of a sequential loop.

**The draft's own site list broke its own invariant.** Site 1 is reached *from* site 2 — it is the
demoted fallback out of `ResolveSlow` — the two `DispatchSequential` overloads call each other, and
the `Publish(object)` route lands in the same generated body. Probing all three naively yields two or
three envelope spans for one publish.

---

## Decision

Add a read-only observation port for notifications; gate it with the machinery that already exists for
custom publishers; and route every path through one observed entry so an envelope cannot be opened
twice.

### The port

```csharp
namespace DSoftStudio.Mediator.Abstractions;

public interface IMediatorNotificationObserver
{
    /// <summary>
    /// Allocation-free, read once per publish. TRUE when ANY gate the adapter owns is live — an
    /// adapter with independent tracing and metrics gates ORs them here, so the core never learns
    /// they are two things and cannot collapse them.
    /// </summary>
    bool IsActive { get; }

    /// <summary>Null means observe nothing; the core then runs the ordinary unobserved dispatch.</summary>
    IMediatorPublishScope? BeginPublish<TNotification>(TNotification notification)
        where TNotification : INotification;
}

public interface IMediatorPublishScope : IDisposable
{
    /// <summary>
    /// Called immediately before <c>handler.Handle(...)</c>, handed the handler INSTANCE so the
    /// adapter can read the concrete type — through <c>IPipelineHandlerTypeAccessor</c> where it
    /// applies — without the core resolving or allocating anything. The returned scope is disposed
    /// only after the handler's whole Task completes, never when <c>Handle</c> returns.
    /// </summary>
    IMediatorSubscriberScope? BeginSubscriber(object handler);

    /// <summary>
    /// The core could not reach the subscriber invocations because a third-party
    /// <see cref="INotificationPublisher"/> owns the loop. Called once instead of
    /// <see cref="BeginSubscriber"/>, so the adapter degrades deliberately rather than leaving a
    /// consumer to read "a publish row with no subscriber rows" as "no handlers ran".
    /// </summary>
    void OnSubscribersUnobservable();

    void OnError(Exception exception);
}

public interface IMediatorSubscriberScope : IDisposable
{
    void OnError(Exception exception);
}
```

`IMediatorDispatchObserver` and `IMediatorDispatchScope` are unchanged; this is additive.

Three things in that shape are answers to measured failures rather than taste. `BeginSubscriber` takes
the **instance** because a wrapper type collapses every subscriber into one and breaks the CPU
self-attribution that matches the handler-type tag verbatim against stack frames. `IMediatorSubscriberScope`
carries its **own** `OnError` because the per-subscriber error status, `error.type` and recorded
exception cannot be expressed from the publish scope. `OnSubscribersUnobservable` exists because the
alternative is silent, wrong data.

### The gate

`NotificationPublisherFlag` is widened **in place** — a second static class would be a second static
base on the hot path — from a bool to a flags word:

```csharp
public static bool NotPlain          { get => Volatile.Read(ref _flags) != 0; }        // the one hot read
public static bool HasCustomPublisher{ get => (Volatile.Read(ref _flags) & 1) != 0; }  // now cold-only
```

`ObserverAppeared()` mirrors the existing `PublisherAppeared()`. `AggressiveNotificationDispatch`
already disqualifies a notification type from the armed tier on a single descriptor — it does exactly
this for `INotificationPublisher` — so observation becomes one added comparison at startup and one
more `&&` in the eligibility check.

This answers the first draft's own rejected alternative. Whether a *listener* is attached cannot be
known at registration time; whether an **observer is registered** can, and that is the fact the gate
needs.

### The routing

One `DispatchRouted<TNotification>` entry in `NotificationCachedDispatcher`, marked `NoInlining`,
carrying today's publisher branch verbatim so handler **membership** does not change. Every route
funnels through it, which is what makes "exactly one envelope per publish" true by construction rather
than by discipline.

### Observed bodies are async; unobserved ones are not

The observed dispatch body must be `async`, mirroring `HandleObserved` on the request side —
`scope?.OnError(ex)` before `scope?.Dispose()` in a `finally`, so a synchronous throw cannot leak an
unstopped Activity. The unobserved body keeps its instruction stream unchanged: the observed variant is
a separate, cold twin, not a probe threaded through the fast one.

---

## Invariants

| # | Invariant | Why |
|---|---|---|
| I1 | `mediator.request.kind` contains `notif` on both spans | the consumer classifier matches by lowercase substring |
| I2 | `mediator.request.type` is the notification `FullName` | pipeline grouping and source-map key |
| I3 | `mediator.handler.type` present on the subscriber, absent on the envelope | the sole discriminator between a subscriber row and a publish row |
| I4 | the subscriber's parent is the envelope | pairs the fan-out with its publish |
| I5 | exactly one envelope per publish | executions are counted from it |
| I6 | the subscriber span is ambient for what that subscriber emits | otherwise its HTTP/DB spans lose their mediator ancestor and are dropped |
| I7 | a per-subscriber error channel | status, `error.type` and a recorded exception, per handler |
| I8 | the subscriber observation covers the handler's **whole Task** | closing at invocation return measured 0.0–1.8 ms subscriber spans against a 157 ms envelope |
| I9 | publishing does not mutate the caller's ambient Activity | otherwise the caller's next span parents to a stopped subscriber span |
| I10 | **Baggage survives into the subscriber and into what it emits** | explicit-context parenting drops it; this is why anchoring is ambient |

---

## Performance budget

Falsifiable, and named rather than gestured at. With **no observer registered**,
`DSoftPublishBenchmarks.DSoft_Publish` on the armed tier must not regress beyond run-to-run noise
against a control row in the same run — this machine drifts up to 12% between runs, so the comparison
is against the control, never against a previously recorded absolute.

The first draft's budget was rejected by its own rule: the case it told reviewers to measure — observer
registered but `IsActive == false` — measured **+1.61 ns / +56% on net10 and +1.35 ns / +63% on net11**.
The gate above exists to make that case cost nothing, by disqualifying the container at startup rather
than testing a field per publish.

The two numbers the first draft quoted were also not comparable: different BenchmarkDotNet versions,
different runtimes, and runtime-async enabled only on net11. Any future quote must name the tier, the
run and the control.

---

## What this does not fix

**The request side keeps its +72%.** An inactive `IMediatorDispatchObserver` still forces a chain for
every request type. The same two-holder treatment would fix it and is the obvious follow-up, but it is
a separate change to a shipped path and belongs in its own ADR.

**The Enterprise profiler also replaces `INotificationPublisher`**, unconditionally. Until it moves to
this port, a live profiling session keeps the disarmed fast path, the changed membership and the
changed instance identity. The port is designed to serve both producers; whether the profiler adopts it
is not this repository's decision.

---

## The measurement that settles it

A vertical slice was built — the three interfaces, the widened flag, `MediatorObservation` and the
routed entry, wired into the virtual `Publish<TNotification>`. No generator support, and the
OpenTelemetry bridge untouched.

`DSoftPublishBenchmarks`, net10.0, the two runs minutes apart on an otherwise idle machine, each with
its own `Direct_Publish` control in the same run:

| | Direct_Publish | DSoft_Publish | overhead | ratio | allocated |
|---|---|---|---|---|---|
| base `1.4.0` | 3.055 ns | 3.312 ns | **+0.257 ns** | 1.08 | 0 B |
| slice | 3.044 ns | 3.281 ns | **+0.237 ns** | 1.08 | 0 B |

A difference of **0.020 ns** against a reported Error of ±0.04 on both rows, with identical ratios and
identical allocations. The port is indistinguishable from not having it on the unobserved path.

That is not a fine measurement flattering a small cost. The unobserved path never reaches the routed
entry: the question is answered at startup, because whether an observer is REGISTERED is knowable
before the container is built, so a process with none pays one static read and one branch — and that
read already existed for custom publishers.

Still unbuilt, and not claimed: generator support for the armed and safe tiers, the bridge migration,
and a test that pins the ambient discipline and the parenting against a real `ActivityListener`.

---

## Alternatives rejected

**Keep replacing the publisher and document it.** Observability would remain a behavioural change, and
the membership difference turns a tracing rollout into an incident.

**Install the decorator only when a listener is active.** Registration happens before the container is
built; a listener can appear afterwards. Superseded in part by the gate above, which uses the fact that
*is* knowable at registration time.

**Emit spans from the core.** The core has no `ActivitySource` and should not acquire one.

**Keep decorating, but dispatch the generated table** so membership, identity and order are preserved.
`NotificationDispatch<T>.Handlers` and `NotificationHandlerCache<T>` are already public, so this needs
no new core API — and it is the first thing a reviewer proposes. It dies because
`INotificationPublisher.Publish` has no `IServiceProvider` parameter, so a singleton decorator cannot
resolve against the calling scope and scoped handlers would resolve from root or throw. Fixing that
means changing a shipped public interface, which is worse than adding new ones. It would not recover
the cost either: the publisher route measured 19.6 ns against 1.85 ns armed, and the interceptor pays
`GetService<INotificationPublisher>` plus `GetServices<INotificationHandler<T>>` *before* the publisher
is called, so a smarter publisher cannot avoid the waste.
