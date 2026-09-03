// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace DSoftStudio.Mediator.Abstractions;

/// <summary>
/// Observation port for notification dispatch: the core CALLS the observer, and the observer never
/// substitutes anything.
/// <para>
/// The distinction is the whole point. Instrumenting a publish by replacing the registered
/// <see cref="INotificationPublisher"/> — the only option before this port existed — changes what it
/// observes: it disarms the generated dispatch, resolves handlers from the container instead of the
/// compile-time table (so a handler the generator could not see goes from skipped to invoked), and
/// hands back a different singleton instance for a handler that keeps state.
/// </para>
/// <para>
/// Registering an implementation is a static, pre-container fact, so the core disqualifies the
/// notification fast path at startup rather than testing for an observer on every publish. An
/// application that registers none pays nothing.
/// </para>
/// </summary>
public interface IMediatorNotificationObserver
{
    /// <summary>
    /// Allocation-free, read once per publish before anything else happens.
    /// <para>
    /// Return <see langword="true"/> when ANY gate the adapter owns is live. An adapter with
    /// independent tracing and metrics gates ORs them here, so the core never learns they are two
    /// things and cannot collapse them — a metrics-only adapter returns <see langword="true"/> and
    /// then returns <see langword="null"/> from <see cref="IMediatorPublishScope.BeginSubscriber"/>.
    /// </para>
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Opens observation for one publish, or returns <see langword="null"/> to observe nothing —
    /// filtered or sampled out — in which case the core runs the ordinary unobserved dispatch.
    /// </summary>
    IMediatorPublishScope? BeginPublish<TNotification>(TNotification notification)
        where TNotification : INotification;
}

/// <summary>
/// One publish. Disposed when the whole publish completes, whether it succeeded or not.
/// <para>
/// <b><see cref="BeginSubscriber"/> may be called concurrently from several threads for the same
/// scope.</b> A publisher is free to run the handlers in parallel — the built-in parallel one queues
/// each to the thread pool — so an implementation must be safe under concurrent calls. The returned
/// subscriber scopes are independent of one another; only this publish scope is shared.
/// </para>
/// <para>
/// <see cref="OnError"/> and <see cref="IDisposable.Dispose"/> are called once each, after every
/// subscriber has finished, so they need no synchronisation of their own.
/// </para>
/// </summary>
public interface IMediatorPublishScope : IDisposable
{
    /// <summary>
    /// How many subscribers this publish resolved, called once the core knows and before any of them
    /// starts. A mandatory member, not a default implementation: this package targets
    /// netstandard2.0, where default interface members have no runtime support.
    /// <para>
    /// It exists because the count and the observation window cannot be delivered together. The
    /// window has to open at the start of the publish, before the handlers are resolved, or it stops
    /// covering the resolution it is meant to measure — so the count cannot ride on
    /// <see cref="IMediatorNotificationObserver.BeginPublish"/>. Without it an adapter can only count
    /// the subscribers that STARTED, which after a failure part-way through the fan-out is a
    /// different and less useful number than how many there were.
    /// </para>
    /// <para>
    /// The contract, so every adapter reads it the same way: called EXACTLY ONCE per scope, after the
    /// subscribers are resolved and before the first <see cref="BeginSubscriber"/>; called with
    /// <c>0</c> when there are none, so "resolved zero" is distinguishable from "never told"; called
    /// on both observed routes; and — unlike <see cref="BeginSubscriber"/> — NOT concurrent, so an
    /// implementation needs no synchronisation of its own.
    /// </para>
    /// </summary>
    void OnSubscribersResolved(int count);

    /// <summary>
    /// Called immediately before <c>handler.Handle(...)</c>, and handed the handler INSTANCE.
    /// <para>
    /// The instance rather than the type on purpose: an adapter must be able to read the CONCRETE
    /// subscriber — through <see cref="IPipelineHandlerTypeAccessor"/> where it applies — and a
    /// wrapper type collapses every subscriber into one, which breaks any correlation keyed on it.
    /// Passing the instance also means the core resolves and allocates nothing to answer the question.
    /// </para>
    /// <para>
    /// The returned scope is disposed only after the handler's whole <see cref="System.Threading.Tasks.Task"/>
    /// completes — never when <c>Handle</c> returns, which for an asynchronous handler is almost
    /// immediately and would report a subscriber that took no time at all.
    /// </para>
    /// </summary>
    IMediatorSubscriberScope? BeginSubscriber(object handler);

    /// <summary>An exception that escaped the entire publish. At most once, before disposal.</summary>
    void OnError(Exception exception);
}

/// <summary>
/// One subscriber invocation. Disposed after that handler's task completes.
/// <para>
/// It carries its own <see cref="OnError"/> because a per-subscriber failure is not the publish
/// failing: one handler can throw while the others succeed, and an adapter has to be able to mark
/// that one rather than the whole publish.
/// </para>
/// </summary>
public interface IMediatorSubscriberScope : IDisposable
{
    /// <summary>The exception this subscriber failed with. At most once, before disposal.</summary>
    void OnError(Exception exception);
}
