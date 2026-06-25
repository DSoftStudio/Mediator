// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// OpenTelemetry adapter for the core's <see cref="IMediatorDispatchObserver"/> port. Opens ONE span per
/// request dispatch that wraps the ENTIRE pipeline — pre-processors, behaviors, handler and post-processors —
/// so every component nests under it. The old <c>MediatorTracingBehavior</c> could not do this: as a pipeline
/// behavior it only saw the behavior chain, while pre-/post-processors run outside it. The mediator stays
/// tracing-agnostic; this is the only place <see cref="Activity"/> is touched on the request path.
/// </summary>
internal sealed class MediatorDispatchTracingObserver(MediatorInstrumentationOptions options) : IMediatorDispatchObserver
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;
    private readonly MediatorInstrumentationOptions _options = options;

    /// <summary>
    /// True only when tracing is enabled AND a listener is attached to our source. <see cref="ActivitySource.HasListeners"/>
    /// is a per-source null/count check (~1 ns) — a listener for ANOTHER source leaves ours untouched — so a
    /// registered-but-unexported bridge keeps the mediator on its fast path.
    /// </summary>
    public bool IsActive => _options.EnableTracing && Source.HasListeners();

    public IMediatorDispatchScope? BeginDispatch<TRequest, TResponse>(TRequest request, IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        // IsActive already gated EnableTracing + HasListeners; only the per-request type filter remains.
        if (_options.Filter is not null && !_options.Filter(typeof(TRequest)))
            return null;

        var activity = Source.StartActivity(
            MediatorTelemetryMetadata<TRequest, TResponse>.SpanName,
            ActivityKind.Internal);

        if (activity is null)
            return null; // sampled out

        if (activity.IsAllDataRequested)
        {
            activity.SetTag("mediator.request.type", MediatorTelemetryMetadata<TRequest, TResponse>.RequestType);
            activity.SetTag("mediator.response.type", MediatorTelemetryMetadata<TRequest, TResponse>.ResponseType);
            activity.SetTag("mediator.request.kind", MediatorTelemetryMetadata<TRequest, TResponse>.RequestKind);
            // ADR-0049 — the concrete handler behind this request, so an imported trace maps the request span to
            // its handler source and renders HTTP/DB child spans as dependencies UNDER it. The pipeline already
            // resolved the right handler and handed it to us; we read its type, never resolve anything.
            activity.SetTag("mediator.handler.type", ResolveHandlerType(handler).FullName);

            _options.EnrichActivity?.Invoke(activity, request!);
        }

        return new DispatchSpanScope(activity, _options);
    }

    /// <summary>
    /// The concrete handler type for this dispatch. The mediator hands us the terminal handler directly; a
    /// chain adapter (when present) exposes the real handler via <see cref="IPipelineHandlerTypeAccessor"/>,
    /// otherwise the runtime type of the handler IS the concrete type. Typed as <see cref="object"/> so this
    /// stays a single non-generic helper (the resolution needs no <c>TRequest</c>/<c>TResponse</c>).
    /// </summary>
    private static Type ResolveHandlerType(object handler)
        => handler is IPipelineHandlerTypeAccessor accessor ? accessor.HandlerType : handler.GetType();

    /// <summary>
    /// Wraps the dispatch span so the mediator can report the outcome without knowing it is a span: success
    /// (Dispose) sets <see cref="ActivityStatusCode.Ok"/>; an unhandled failure (<see cref="OnError"/>) sets
    /// the error status and records the exception per OTel semantic conventions.
    /// </summary>
    private sealed class DispatchSpanScope(Activity activity, MediatorInstrumentationOptions options) : IMediatorDispatchScope
    {
        private bool _errored;

        public void OnError(Exception exception)
        {
            _errored = true;
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("error.type", exception.GetType().FullName);
            ActivityHelper.RecordException(activity, exception, options.RecordExceptionStackTraces);
        }

        public void Dispose()
        {
            if (!_errored)
                activity.SetStatus(ActivityStatusCode.Ok);
            activity.Dispose();
        }
    }
}
