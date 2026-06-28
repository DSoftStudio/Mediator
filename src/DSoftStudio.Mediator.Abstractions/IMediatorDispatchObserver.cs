// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace DSoftStudio.Mediator.Abstractions;

/// <summary>
/// Optional observation port for the request-dispatch boundary (Ports &amp; Adapters).
/// <para>
/// The mediator itself does NOT observe or trace — it merely EXPOSES the dispatch lifecycle so an external
/// adapter (e.g. the OpenTelemetry bridge) can wrap the WHOLE pipeline: pre-processors, behaviors, handler
/// and post-processors. A pipeline behavior cannot do this, because pre-/post-processors run OUTSIDE the
/// behavior chain — so the only place a span can nest every component is the dispatch boundary the mediator
/// owns. This keeps the core tracing-agnostic (no <c>System.Diagnostics.Activity</c> dependency): the core
/// defines the port; the bridge is the adapter.
/// </para>
/// <para>
/// Cost contract: when no observer is registered (the common case) the mediator pays nothing — the dispatch
/// stays on its fast path. When one IS registered, the mediator first reads <see cref="IsActive"/> (cheap,
/// allocation-free) and only calls <see cref="BeginDispatch{TRequest,TResponse}"/> when something is
/// actually observing — so a registered-but-idle adapter (bridge present, no exporter attached) adds no
/// hot-path cost beyond a single property read.
/// </para>
/// </summary>
public interface IMediatorDispatchObserver
{
    /// <summary>
    /// Cheap, allocation-free check for whether anything is observing right now (e.g. an active tracing
    /// listener/exporter). The mediator skips wrapping the dispatch entirely when this is <see langword="false"/>.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Called at the START of a request dispatch, BEFORE pre-processors run. The returned scope is disposed
    /// when the ENTIRE dispatch completes (after post-processors), so the adapter can open a span that nests
    /// every pipeline component. Returns <see langword="null"/> to observe nothing for this dispatch (e.g. the
    /// adapter filtered this request type out, or sampling dropped it).
    /// </summary>
    /// <param name="request">
    /// The request being dispatched — lets the adapter enrich the observation with request-specific data
    /// (e.g. custom span tags) without the mediator knowing what enrichment means.
    /// </param>
    /// <param name="handler">
    /// The terminal handler for this dispatch — lets the adapter record the concrete handler type without the
    /// mediator resolving anything (the adapter inspects it, e.g. via <see cref="IPipelineHandlerTypeAccessor"/>).
    /// </param>
    IMediatorDispatchScope? BeginDispatch<TRequest, TResponse>(TRequest request, IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>;
}

/// <summary>
/// The lifetime scope of a single observed dispatch, returned by
/// <see cref="IMediatorDispatchObserver.BeginDispatch{TRequest,TResponse}"/>.
/// <para>
/// <see cref="IDisposable.Dispose"/> is called when the dispatch completes (success OR failure), ending the
/// observation. The mediator reports an unhandled failure via <see cref="OnError"/> BEFORE disposing, so the
/// adapter can mark the observation (e.g. set the span status to error and record the exception). The notion
/// of "the dispatch failed with this exception" is generic dispatch-outcome data — not a tracing concept —
/// so reporting it keeps the core tracing-agnostic.
/// </para>
/// </summary>
public interface IMediatorDispatchScope : IDisposable
{
    /// <summary>
    /// Reports that the dispatch failed with an exception that propagated past every pipeline component
    /// (including exception handlers). Called at most once, just before <see cref="IDisposable.Dispose"/>.
    /// Not called when the dispatch completes successfully.
    /// </summary>
    void OnError(Exception exception);
}
