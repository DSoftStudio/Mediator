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
