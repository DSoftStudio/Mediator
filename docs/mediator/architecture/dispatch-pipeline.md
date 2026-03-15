---
layout: default
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Dispatch Pipeline

## Runtime Execution Path

The hot path for `Send()` resolves a transient `PipelineChainHandler` from DI:

```
Mediator.Send<TRequest, TResponse>()
   │
   ▼
serviceProvider.GetRequiredService<PipelineChainHandler<TRequest, TResponse>>()
   │
   ▼
[no behaviors] → handler.Handle()                 ← fast path: direct call
[N behaviors]  → PipelineChainHandler (index++)   ← zero-alloc: interface dispatch, no closures
```

The runtime-typed `Send(object)` path uses a separate dispatch table:

```
SenderObjectExtensions.Send(ISender, object)
   │
   ▼
RequestObjectDispatch.Dispatch(Type, object, IServiceProvider, CancellationToken)
   │
   ▼
FrozenDictionary<Type, DispatchDelegate> lookup
   │
   ▼
Delegate casts object → TRequest, enters the same pipeline as Send<TRequest, TResponse>()
```

Service resolution goes directly through `IServiceProvider` — the standard DI container call that every mediator must make. This avoids additional container abstractions or service locators.

> **Note:** Backward-compatible overloads (`Send<TResponse>(IRequest<TResponse>)`, `Publish(object)`, and `CreateStream`) use a one-time reflection + `ConcurrentDictionary` cache on first call per type. Subsequent calls hit the cache with zero reflection. The `Send(object)` overload uses a precompiled `FrozenDictionary<Type, DispatchDelegate>` — no reflection at any point. Prefer the strongly-typed overloads for maximum performance.

## What Is Registered at Startup

- **`RegisterMediatorHandlers()`** — registers handlers with automatic lifetime selection: **Singleton** for stateless handlers (no constructor parameters), **Transient** for handlers with DI dependencies. This eliminates per-call allocation for stateless handlers while preserving correct DI semantics for handlers that inject services.
- **`PrecompilePipelines()`** — registers `PipelineChainHandler<TRequest, TResponse>` for every request type that has pipeline components (behaviors, pre/post processors, exception handlers). The chain's lifetime is determined by its components: Singleton when all are Singleton, Scoped when any is Scoped, Transient when any is Transient. Also freezes `RequestObjectDispatch` — the `FrozenDictionary<Type, DispatchDelegate>` used by `Send(object)` for runtime-typed dispatch.
- **`PrecompileNotifications()`** — populates `NotificationDispatch<T>.Handlers` static arrays with factory delegates for each notification type.
- **`PrecompileStreams()`** — populates `StreamDispatch<TRequest, TResponse>.Handler` static factory delegates for each stream type.

## What Runs per Request

- For requests **without behaviors**, the interceptor resolves the handler directly from DI — Singleton handlers return the cached instance (zero allocation), Transient handlers create a new instance
- For requests **with behaviors**, a `PipelineChainHandler` is resolved from DI — it passes itself as the `next` parameter to each behavior via interface dispatch, advancing through the behavior array without allocating closures or delegates
- Pre/post processors and exception handlers execute around the core pipeline when registered

This design ensures correct lifetime semantics: Singleton handlers are shared across all calls (safe for stateless handlers), Transient handlers get new instances per call (safe for handlers with DI dependencies), and Scoped handlers are shared within the HTTP request scope. Users can always override the auto-detected lifetime by re-registering after `RegisterMediatorHandlers()` — the last registration wins.

## What This Means at Runtime

- **Handler discovery** is done at compile time — no assembly scanning, no `GetTypes()`, no attribute reflection.
- **Request dispatch** resolves a new `PipelineChainHandler` from DI per `Send()` call. The handler, behaviors, pre/post processors, and exception handlers are injected by the container.
- **Runtime-typed request dispatch** (`Send(object)`) looks up a `FrozenDictionary<Type, DispatchDelegate>` by the request's runtime `Type`, casts `object` → `TRequest`, and enters the same pipeline as `Send<TRequest, TResponse>()`. No reflection, AOT-safe.
- **Notification dispatch** reads a precompiled `Func<IServiceProvider, INotificationHandler<T>>[]` array — no `GetServices<T>()` enumeration per publish.
- **Stream dispatch** resolves handlers through a precompiled factory delegate stored in `StreamDispatch<TRequest, TResponse>.Handler`.

The result is that every `Send()`, `Send(object)`, `Publish()`, and `CreateStream()` call at runtime uses precompiled dispatch tables with zero discovery overhead.
