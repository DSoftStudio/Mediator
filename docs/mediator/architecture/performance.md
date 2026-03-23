---
layout: default
title: "Performance Architecture - DSoftStudio.Mediator"
description: "Deep dive into zero-allocation performance architecture."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Performance Design

DSoftStudio.Mediator achieves near-direct-call latency (~7 ns Send, ~0.6 ns overhead) through a combination of compile-time code generation and runtime-free dispatch. This document explains the key design decisions that make it the fastest .NET mediator tested.

## Compile-Time Dispatch

Source generators analyze your handlers at build time and emit strongly-typed dispatch methods. When you call `mediator.Send(new Ping())`, the compiler resolves the exact handler type — no dictionary lookup, no `Type.GetType()`, no `MakeGenericType()`.

```
Build time:
  Source Generator → discovers PingHandler
                   → emits direct call: handler.Handle(request, ct)

Runtime:
  Send(new Ping()) → compiled dispatch → PingHandler.Handle()
```

This is fundamentally different from reflection-based mediators that resolve handlers at runtime via `IServiceProvider.GetService(typeof(IRequestHandler<,>).MakeGenericType(...))`.

## Zero-Allocation Pipeline Chain

Pipeline behaviors are chained via **interface dispatch**, not delegate allocation. Each behavior receives the next handler as `IRequestHandler<TRequest, TResponse>` rather than a `Func<>` or `RequestHandlerDelegate<>`:

```csharp
// DSoftStudio.Mediator — interface dispatch (zero allocation)
public ValueTask<TResponse> Handle(
    TRequest request,
    IRequestHandler<TRequest, TResponse> next,  // no allocation
    CancellationToken ct)

// MediatR — delegate allocation (heap allocation per call)
public Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,     // allocates delegate
    CancellationToken ct)
```

The difference: interface dispatch is a virtual method call (near-zero cost), while delegate creation allocates on the heap every time the pipeline executes.

## FrozenDictionary for Runtime Dispatch

`Send(object)` uses `FrozenDictionary<Type, Func<...>>` — a read-optimized, immutable dictionary built once at startup. Lookups are faster than `Dictionary<>` and allocation-free, while remaining fully AOT-compatible (no reflection at runtime).

## Auto-Singleton Handler Cache

Handlers without constructor parameters are automatically registered as **Singleton**. This eliminates per-call `IServiceProvider.GetService()` resolution and object allocation:

| Registration | Per-call cost |
|---|---|
| Transient (MediatR default) | `GetService()` + `new Handler()` |
| **Singleton (DSoft auto)** | Cached reference — zero allocation |

The source generator detects whether a handler has constructor parameters and emits the appropriate registration.

## ValueTask Return Type

All handlers return `ValueTask<T>` instead of `Task<T>`. When the result is available synchronously (common for in-memory operations, cache hits, validation failures), `ValueTask<T>` avoids the `Task` heap allocation entirely.

## Notification Dispatch by Exact Type

Notifications are dispatched by **exact compile-time type**, not by walking the inheritance hierarchy at runtime. This means:

- No `Type.GetInterfaces()` reflection
- No risk of duplicate handler invocation (a known MediatR issue with polymorphic notifications)
- O(1) dispatch instead of O(n) interface scanning

## Pipeline Precompilation

`PrecompilePipelines()` resolves and chains all pipeline behaviors at startup, so the first `Send()` call is as fast as subsequent calls — no lazy initialization, no lock contention, no JIT surprise on the first request.

## Interceptor Code Generation (Release vs Debug)

The source generators emit C# [interceptors](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12#interceptors) that replace `ISender.Send`, `IPublisher.Publish`, and `IMediator.CreateStream` call sites at compile time with direct pipeline invocations — eliminating virtual dispatch entirely.

The generated code adapts to the build's **`OptimizationLevel`**:

| Build | Generated pattern | Overhead vs direct call | Mock-safe |
|---|---|---|---|
| **Release** | Branchless `castclass IServiceProviderAccessor` | ~0.6 ns | ❌ throws `InvalidCastException` on mocks |
| **Debug** | `is not IServiceProviderAccessor` + virtual fallback | ~3 ns | ✅ graceful fallback to virtual dispatch |

A single `isinst` + branch instruction prevents the JIT's Guarded Devirtualization (GDV) from fully devirtualizing the interface call, adding ~3 ns on every invocation. The branchless `castclass` pattern in Release lets GDV optimize the dispatch to a method-table pointer comparison + direct field load — effectively zero overhead.

In **Debug** builds (where tests typically run), the generated interceptors detect test doubles (Moq, NSubstitute, etc.) that don't implement `IServiceProviderAccessor` and fall back to virtual dispatch.

**Typed extensions** (e.g. `Send(Ping)`) always use the defensive `isinst` + fallback pattern regardless of build configuration — they are the fallback path when interceptors are suppressed and must remain mock-safe in `dotnet test -c Release` CI pipelines. The ~1–2 ns overhead is negligible. See [Design Notes](design-notes.md) for the full comparison table.

> **Tip:** If you mock `ISender` in a project that references the generator and build in Release mode, the interceptor will throw `InvalidCastException`. Set `<DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors>` or reference only `DSoftStudio.Mediator.Abstractions` in your test project. See [Source Generators](source-generators.md) for DSOFT004.

## Publish Interceptor — `NotificationPublisherFlag` Bypass

Most applications never register a custom `INotificationPublisher`. Without optimization, every generated `Publish` interceptor would call `GetService<INotificationPublisher>()` on every invocation — even when the result is always `null`. That DI probe alone costs ~3–4 ns per call.

`NotificationPublisherFlag` is a write-once global `Volatile` boolean that eliminates this probe:

1. **Default path (no custom publisher):** The flag is `false`. The generated interceptor reads `HasCustomPublisher` (~0.1 ns) and short-circuits directly to `NotificationCachedDispatcher.DispatchSequential` — zero DI lookup.
2. **Custom publisher path:** When `INotificationPublisher` is registered (e.g. `ParallelNotificationPublisher`, OpenTelemetry's `InstrumentedNotificationPublisher`), the `Mediator` constructor calls `MarkRegistered()` once. All subsequent interceptors see the flag and take the `GetService` path as before.

```
Publish(notification)
  │
  ▼
NotificationPublisherFlag.HasCustomPublisher?  (~0.1 ns Volatile.Read)
  │
  ├─ false ──▶ NotificationCachedDispatcher.DispatchSequential()   ← fast path
  │
  └─ true  ──▶ GetService<INotificationPublisher>()                ← custom path
               └─▶ customPublisher.Publish(handlers, notification)
```

| Scenario | Before optimization | After optimization |
|---|---|---|
| No custom publisher (default) | `GetService` returns `null` (~3–4 ns) | `Volatile.Read` (~0.1 ns) |
| Custom publisher registered | `GetService` returns instance (~3–4 ns) | `Volatile.Read` + `GetService` (~3–4 ns) |

Net effect: **~4 ns saved per `Publish` call** in the default (no custom publisher) path — bringing the Publish interceptor from ~2.2× to ~1.1× overhead vs direct dispatch.

## Summary

| Technique | Impact |
|---|---|
| Compile-time dispatch | Eliminates reflection + dictionary lookup |
| Interface dispatch pipeline | Zero delegate allocation per call |
| FrozenDictionary | Fast AOT-safe runtime dispatch |
| Auto-Singleton handlers | Eliminates per-call object creation |
| ValueTask returns | Avoids Task allocation for sync paths |
| Exact-type notification dispatch | O(1) dispatch, no duplicate handlers |
| Pipeline precompilation | Eliminates cold-start penalty |
| Interceptor code generation | Release: ~0.6 ns overhead (GDV-optimized) |
| `NotificationPublisherFlag` bypass | Skips DI probe when no custom publisher (~4 ns saved) |
