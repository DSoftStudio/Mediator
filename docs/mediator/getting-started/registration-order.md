---
layout: default
title: "Registration Order - DSoftStudio.Mediator"
description: "Understand the correct DI registration order for handlers and pipelines."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Registration Order

## Recommended: `AddMediator(configure)` (v1.2.0+)

The simplest way to register the mediator is the single-call `AddMediator(configure)` overload. It handles the entire registration sequence automatically:

```csharp
services.AddMediator(builder =>
{
    builder.AddOpenBehavior(typeof(LoggingBehavior<,>));
    builder.AddRequestPreProcessor<ValidationPreProcessor>();
    builder.AddRequestPostProcessor<AuditPostProcessor>();
    builder.AddRequestExceptionHandler<GlobalExceptionHandler>();
    builder.AddParallelNotificationPublisher();
});
```

Internally, this single call executes in order:
1. `AddMediator()` — registers core services (`IMediator`, `ISender`, `IPublisher`)
2. `RegisterMediatorHandlers()` — discovers and registers all handlers
3. `configure(builder)` — your pipeline customization
4. `PrecompilePipelines()` + `Freeze()` — precompiles dispatch tables

> **Do not mix `AddMediator(configure)` with individual registration calls.**
> Calling `RegisterMediatorHandlers()` or `PrecompilePipelines()` separately alongside
> `AddMediator(configure)` causes redundant registrations. The compile-time diagnostic
> **DSOFT007** will warn you if mixed usage is detected. Runtime idempotency guards prevent
> actual double-registration, but the intent should be clear in your code.

## Alternative: Step-by-step Registration

If you need fine-grained control over the registration order, use the individual methods.
The `Precompile*` methods inspect the `IServiceCollection` at startup to determine dispatch strategies and chain lifetimes. **All service registrations must happen before the corresponding `Precompile*` call.**

> **Why this is a hard rule.** The `Precompile*` calls are a **freeze point**, by design: they read
> the collection once, decide the dispatch strategy and the chain lifetimes from what they find, and
> seal the result so nothing that comes later can perturb what was already built. That is what makes
> dispatch a static-field read at runtime instead of a lookup.
>
> The consequence is that registration order is part of the contract. The scan decides, per
> request/response pair, whether a pipeline chain is built *at all*. If a pair has no behavior,
> processor or exception handler registered by then, no chain exists and anything added afterwards
> never runs. Calling `PrecompilePipelines()` a second time does not re-open the decision — it returns
> immediately, on purpose, so a late registration cannot half-rebuild a pipeline other code is already
> dispatching through.
>
> Late registration used to be silent. It is now reported in two places: **DSOFT010** at compile time
> when the registration and the scan are in the same method, and `ValidateMediatorHandlers()` at
> startup for everything else — including registrations in another method or another assembly, and
> components captured under a lifetime the scan fixed before they existed.
>
> The scan records only that a chain is needed; the components themselves are resolved from the
> container when the chain is constructed. So a behavior added after the scan *does* run when the
> pair already had one — but the chain's lifetime was fixed by the scan, so a `Transient` component
> added late can end up constructed once and shared. Register the whole pipeline up front.

```csharp
services
    .AddMediator()                // 1. Core mediator services
    .RegisterMediatorHandlers();  // 2. Source-generated handler registrations

// 3. Register behaviors, processors, exception handlers
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IRequestPreProcessor<>), typeof(ValidationPreProcessor<>));
services.AddScoped(typeof(IRequestPostProcessor<,>), typeof(AuditPostProcessor<,>));

// 4. Override handler lifetimes (optional)
services.AddScoped<IRequestHandler<MyRequest, MyResponse>, MyHandler>();

// 5. Register notification strategies (optional)
services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();

// 6. Precompile — inspects all registrations above
services
    .PrecompilePipelines()        // scans for IPipelineBehavior, Pre/Post processors, exception
                                  // handlers, IMediatorDispatchObserver, and the handler's lifetime
    .PrecompileNotifications()    // builds static dispatch arrays for each INotification type
    .PrecompileStreams();         // scans for IStreamPipelineBehavior and the stream handler's
                                  // lifetime; builds static factory delegates per IStreamRequest type
```

## What Each Method Inspects

| Method | What it inspects | What to register before |
|---|---|---|
| `PrecompilePipelines()` | `IPipelineBehavior<,>`, `IRequestPreProcessor<>`, `IRequestPostProcessor<,>`, `IRequestExceptionHandler<,>`, `IMediatorDispatchObserver`, handler lifetimes | Behaviors, processors, exception handlers, dispatch observers, handler overrides |
| `PrecompileNotifications()` | `INotificationHandler<>` | Notification handler overrides |
| `PrecompileStreams()` | `IStreamRequestHandler<,>`, `IStreamPipelineBehavior<,>` | Stream handler overrides, **stream behaviors** |

## Pipeline Chain Lifetimes

`PrecompilePipelines()` gives each `PipelineChainHandler` a lifetime that is safe for everything its
constructor consumes — which is more than the components. The request handler and any registered
`IMediatorDispatchObserver` are chain dependencies too, and each constrains it:

| Registered | Chain lifetime |
|---|---|
| every component, the handler and every observer Singleton | **Singleton** — one chain for the process |
| any component, the handler, or an observer, Transient | **Transient** — re-resolved and re-linked on every dispatch, never cached |
| anything else | **Scoped** — one chain per scope, reused by every dispatch in it |

A Transient handler is not an accident to be optimised away. `HandlerLifetimeOptimizer` leaves a
handler Transient when a dependency of its own is transient — when a fresh instance per resolve is the
point — or when a dependency is not registered at all, so caching the chain around it would share
something whose lifetime cannot be seen. An open-generic framework registration does NOT count as
unregistered: a closed `ILogger<T>` or `IOptions<T>` falls back to its open descriptor, so a handler
that only injects a logger is promoted rather than pinned. Container intrinsics — `IServiceProvider`,
`IServiceScopeFactory` — have no descriptor and do still read as unregistered.

`PrecompileStreams()` folds the stream handler and the stream behaviors the same way.

The `Precompile*` calls fix these lifetimes, and nothing registered afterwards re-opens the decision.
A component added after the scan still *runs* when that request already had a chain — the chain
resolves its components from the container — but under the lifetime the scan already chose, so a late
`Transient` component ends up constructed once and shared. A pair that had no chain at scan time never
gets one.
