![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

# DSoftStudio.Mediator.OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.OpenTelemetry.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

OpenTelemetry instrumentation for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). Distributed tracing and metrics for `Send`, `Publish` and `CreateStream`, attached through the core's observation ports rather than by replacing anything you registered.

## Features

- **Distributed tracing** — Activity spans for `Send`, `Publish` and `CreateStream` with semantic attributes
- **Per-handler notification spans** — one span for the publish, one per handler it resolved, each tagged with `mediator.handler.type`
- **Metrics** — `mediator.request.duration` histogram (seconds, with sub-second buckets) plus `mediator.request.active` / `mediator.request.errors` counters, created from the DI `IMeterFactory`
- **Database dependency enrichment** — automatically tags database spans with a redaction-safe `db.operation.name` / `db.sql.table` / `db.stored_procedure.name`, so each query (`SELECT`, `INSERT`, `CALL`…) shows as its own dependency instead of one aggregated row — no configuration
- **Configurable filtering** — Include or exclude specific request types via `MediatorInstrumentationOptions`
- **Zero configuration** — Works out of the box with any OpenTelemetry exporter

## How it attaches

Worth knowing, because it decides what the spans can see and what the package changes about your container.

| Operation | Mechanism |
|---|---|
| `Send` | `IMediatorDispatchObserver` — the core's observation port |
| `Publish` | `IMediatorNotificationObserver` — same idea, for notifications |
| `CreateStream` | `IStreamPipelineBehavior` — streams have no dispatch port |
| Metrics | `IPipelineBehavior` |

The request span is opened at the **dispatch boundary**, not inside the behavior chain, so it wraps the
whole pipeline — pre- and post-processors included. A behavior cannot do that: they run outside it.

Publishing is observed rather than intercepted. Earlier versions replaced `INotificationPublisher`,
which was never neutral — it disarmed the generated dispatch, resolved handlers from the container
instead of the compile-time table (so a handler the generator could not see went from skipped to
invoked), and handed back a different instance for a stateful singleton. The observer substitutes
nothing. The publisher is only decorated when **you** registered your own, because at that point the
fast path is already disarmed and decorating changes nothing that was not already changed.

An application that registers no instrumentation pays nothing: the core takes the observers as an
`IEnumerable` and the empty case is known before the container is built.

## Installation

```shell
dotnet add package DSoftStudio.Mediator.OpenTelemetry
```

## Quick Start

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorInstrumentation()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

services
    .AddOpenTelemetry()
    .WithTracing(b => b.AddMediatorInstrumentation())
    .WithMetrics(b => b.AddMediatorInstrumentation());
```


> The three `Precompile*` calls arm three separate dispatch tables. `PrecompilePipelines()` is the
> one this package needs, but stopping there leaves the rest of your application without
> notification or stream dispatch — and the two fail differently: `CreateStream` throws and names
> the missing call, while `Publish` silently reaches no handlers. `AddMediator(builder => { })`
> calls all three for you.

## Registration order

`AddMediatorInstrumentation()` registers the stream tracing and metrics behaviors as *open* generics.
`PrecompilePipelines()` is the step that closes every open-generic behavior over the discovered
request/response pairs and builds the pipeline chains, so the call has to sit **after
`RegisterMediatorHandlers()` and before `PrecompilePipelines()`** — as in the Quick Start above.

Get the order wrong and **nothing on the request side is instrumented at all** — not the metrics
behavior, and not the request span either.

Being an observer rather than a behavior does not make the dispatch observer independent of this
ordering. `PipelineChainHandler` is its only consumer, and `Mediator.Send` only reaches that handler
when a chain exists:

```csharp
if (RequestDispatch<TRequest, TResponse>.HasPipelineChain)
{
    var chain = PipelineChainCache<TRequest, TResponse>.Resolve(_serviceProvider);
    if (chain is not null)
        return chain.Handle(request, cancellationToken);   // the only path that has the observer
}

return HandlerCache<TRequest, TResponse>.Resolve(_serviceProvider)
    .Handle(request, cancellationToken);                   // no chain, no observer, no span
```

With no behavior closed into a chain, a request goes straight to its handler and the observer is never
constructed. Request spans, stream spans and metrics all disappear together. Notification spans are
the exception and survive, because publishing does not go through a pipeline chain.

> **It can hide in the IDE and surface in production.** Anything else that registers a pipeline
> component — the Pipeline Explorer's profiling mode, for one — forces a chain to exist, and the
> request spans come back. A developer profiling locally sees a complete trace, exports a production
> trace built without it, and finds the requests missing.

The analyzer reports **DSOFT010** when it can see the mistake, which means both calls in the same
method on the same service collection: the Program.cs shape. A registration made from another method
or another assembly is beyond what any analyzer can order, so keep the calls in one chain where the
order is visible.

Calling `AddMediatorInstrumentation()` twice on the same collection is safe — the second call sees a
marker the first left behind and returns. Without that, a second call would register a second set of
observers and every span would be emitted twice.

## Configuration

```csharp
services.AddMediatorInstrumentation(options =>
{
    // Skip noisy request types (e.g. health checks).
    options.Filter = type => !type.Name.Contains("HealthCheck");

    // Tracing and metrics are both ON by default — turn one off if you only want the other.
    // options.EnableTracing = false;
    // options.EnableMetrics = false;

    // Keep error.type on the span but drop the (verbose) exception stack trace.
    options.RecordExceptionStackTraces = false;
});
```

## Documentation

📖 [Full documentation](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
