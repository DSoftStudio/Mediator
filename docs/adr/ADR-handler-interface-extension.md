# ADR: Handler Interface Extension for ValueTask and ValueTask\<Unit\>

**Status:** Rejected  
**Date:** 2025-07-09  
**Supersedes:** Original proposal (draft)

---

## Context

Some users expect a dedicated `IRequestHandler<TRequest>` interface that returns `ValueTask`
(non-generic) for commands that produce no meaningful result, instead of using the current
`IRequestHandler<TRequest, Unit>` → `ValueTask<Unit>` pattern.

The proposal was to add:

```csharp
// Proposed — NOT implemented
public interface IRequestHandler<in TRequest> where TRequest : IRequest
{
    ValueTask Handle(TRequest request, CancellationToken cancellationToken);
}
```

This ADR records why the proposal was evaluated and ultimately rejected.

---

## Current Architecture

The entire request pipeline is built around a **closed, uniform generic chain**:

```
IRequest<TResponse>
  → IRequestHandler<TRequest, TResponse>        → ValueTask<TResponse>
  → IPipelineBehavior<TRequest, TResponse>       → ValueTask<TResponse>
  → IRequestPreProcessor<TRequest>               → ValueTask          (already void)
  → IRequestPostProcessor<TRequest, TResponse>   → ValueTask          (already void)
  → IRequestExceptionHandler<TRequest, TResponse>
  → PipelineChainHandler<TRequest, TResponse>    → ValueTask<TResponse>
  → RequestDispatch<TRequest, TResponse>         (static-generic dispatch table)
  → HandlerCache<TRequest, TResponse>            (ThreadStatic cache)
  → PipelineChainCache<TRequest, TResponse>      (ThreadStatic cache)
  → ISender.Send<TRequest, TResponse>            → ValueTask<TResponse>
```

For void commands the canonical pattern is `TResponse = Unit`:

```csharp
public class DeleteUser : IRequest<Unit> { public int Id { get; init; } }

public class DeleteUserHandler : IRequestHandler<DeleteUser, Unit>
{
    public ValueTask<Unit> Handle(DeleteUser request, CancellationToken ct)
    {
        // ... delete logic ...
        return Unit.ValueTask;   // static field read — zero alloc, one IL instruction
    }
}
```

`Unit.ValueTask` is a `static readonly ValueTask<Unit>` pre-computed at class load.
No allocation, no boxing, no async state machine.

---

## Decision

**Rejected.** The `IRequestHandler<TRequest>` → `ValueTask` extension will not be
implemented. The existing `IRequestHandler<TRequest, Unit>` → `ValueTask<Unit>` pattern
is retained as the only supported void handler signature.

---

## Rationale

### 1. Zero Measurable Performance / Allocation Benefit

| Metric | `ValueTask<Unit>` (current) | `ValueTask` (proposed) | Delta |
|--------|----------------------------|------------------------|-------|
| Heap allocation (sync path) | **0 bytes** | **0 bytes** | **0** |
| Heap allocation (async path) | 1 state machine | 1 state machine | **0** |
| Stack footprint | ~24 bytes | ~16 bytes | **~8 bytes** |
| Sync fast path (`IsCompletedSuccessfully`) | ✅ | ✅ | identical |

`Unit` is a zero-field `readonly struct` (1 byte minimum by CLR rules). The ~8 byte
stack difference per call is irrelevant compared to the cost of the call itself
(~0.5 ns virtual dispatch). There is **no heap allocation difference** — the metric
that actually matters for GC pressure and throughput.

### 2. High Implementation Cost — Two Possible Approaches, Both Costly

#### Option A: Adapter Pattern

Wrap `ValueTask` → `ValueTask<Unit>` at the dispatch boundary:

```csharp
// Would be source-generated
internal sealed class VoidHandlerAdapter<TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    private readonly IVoidRequestHandler<TRequest> _inner;

    public async ValueTask<Unit> Handle(TRequest request, CancellationToken ct)
    {
        await _inner.Handle(request, ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
```

**Problems:**
- Adds an **extra async state machine** (~72–96 bytes) on every async void request — a
  regression in the metric DSoftStudio.Mediator is specifically designed to minimize.
- **Breaks the sync fast path** in `PipelineChainHandler`: the adapter's
  `async ValueTask<Unit>` always allocates a state machine, even when the inner handler
  completes synchronously. The current `return Unit.ValueTask` (cached static) avoids
  this entirely.
- Every `IPipelineBehavior<TRequest, Unit>` and `IRequestPostProcessor<TRequest, Unit>`
  still receives `Unit` — the adapter doesn't simplify consuming code.

#### Option B: Dual Pipeline

Duplicate the entire pipeline infrastructure for a `TResponse`-less variant:

| Component to duplicate | Current | Would need void variant |
|----------------------|---------|------------------------|
| `PipelineChainHandler<TRequest, TResponse>` | ✅ | `PipelineChainHandler<TRequest>` |
| `BehaviorHandlerAdapter<TRequest, TResponse>` | ✅ | `BehaviorHandlerAdapter<TRequest>` |
| `RequestDispatch<TRequest, TResponse>` | ✅ | `RequestDispatch<TRequest>` |
| `HandlerCache<TRequest, TResponse>` | ✅ | `HandlerCache<TRequest>` |
| `PipelineChainCache<TRequest, TResponse>` | ✅ | `PipelineChainCache<TRequest>` |
| `IPipelineBehavior<TRequest, TResponse>` | ✅ | `IPipelineBehavior<TRequest>` |
| `IRequestPostProcessor<TRequest, TResponse>` | ✅ | Not applicable (no response) |
| `IRequestExceptionHandler<TRequest, TResponse>` | ✅ | `IRequestExceptionHandler<TRequest>` |
| Source generators (DI, Pipeline, Validator) | ✅ | Dual codegen paths |

**Problems:**
- **Massive code duplication** — every component, test, and generator path doubles.
- **Maintenance burden** — every future feature (new pipeline stage, AOT improvement,
  trimming annotation) must be implemented twice.
- **Zero runtime benefit** — Option B achieves the same zero-alloc outcome as the
  current `Unit` pattern, but with double the code surface.

### 3. Breaks Uniform Generic Constraint Chain

The pipeline's type safety comes from `TResponse` flowing through every component:

```
IRequest<TResponse> → IRequestHandler<TRequest, TResponse> → IPipelineBehavior<TRequest, TResponse>
```

Introducing a `TResponse`-less variant creates two parallel type hierarchies that
cannot share behaviors, post-processors, or exception handlers. A
`LoggingBehavior<TRequest, TResponse>` would need a separate
`LoggingBehavior<TRequest>` — or a constraint-based abstraction that re-introduces
the complexity we're trying to avoid.

### 4. `Unit` Pattern Is Industry Standard

| Library | Void pattern | Notes |
|---------|-------------|-------|
| **MediatR** | `IRequest` → `Unit` | Identical to ours |
| **Brighter** | `IRequest` → implicit void | Uses `Unit` internally |
| **Wolverine** | `void` return | Different architecture (codegen runtime, not compile-time pipeline) |
| **MassTransit Mediator** | `void` consumers | No pipeline concept — not comparable |

The `Unit` pattern is well-established in the .NET mediator ecosystem and familiar
to developers migrating from MediatR.

### 5. Pre/PostProcessors Already Use `ValueTask` (Non-Generic)

Components that are genuinely "void" already return `ValueTask`:

```csharp
public interface IRequestPreProcessor<in TRequest>
{
    ValueTask Process(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestPostProcessor<in TRequest, in TResponse>
{
    ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
```

The separation is intentional: processors are side-effects (no return value),
while handlers are the core computation (always produce `TResponse`, even if
`TResponse = Unit`). This distinction makes the architecture clearer, not less ergonomic.

### 6. Ergonomic Impact Is Minimal

The only developer-facing difference:

```csharp
// Current (retained)
public class MyCommand : IRequest<Unit> { }
public class MyHandler : IRequestHandler<MyCommand, Unit>
{
    public ValueTask<Unit> Handle(MyCommand r, CancellationToken ct)
        => Unit.ValueTask;
}

// Proposed (rejected)
public class MyCommand : IRequest { }
public class MyHandler : IRequestHandler<MyCommand>
{
    public ValueTask Handle(MyCommand r, CancellationToken ct)
        => default;
}
```

The difference is ~20 characters across the entire handler definition. With IDE
code completion and snippets, this is negligible.

---

## Alternatives Considered

| Alternative | Outcome |
|------------|---------|
| Adapter pattern (`ValueTask` → `ValueTask<Unit>`) | Rejected — adds async state machine overhead, breaks sync fast path |
| Dual pipeline (parallel void infrastructure) | Rejected — massive code duplication, zero runtime benefit |
| Keep `Unit` pattern (status quo) | **Accepted** — zero alloc, zero overhead, industry standard, uniform type chain |

---

## Consequences

- **No code changes required.** The current `IRequestHandler<TRequest, Unit>` →
  `ValueTask<Unit>` pattern remains the only supported void handler signature.
- **Pipeline uniformity preserved.** All components share the same
  `<TRequest, TResponse>` generic chain — no dual paths, no adapters.
- **Zero allocation baseline maintained.** `Unit.ValueTask` is a cached static field
  (72 bytes Send, 0 bytes Publish — verified by allocation regression tests).
- **Documentation clarity.** README should document the `Unit` pattern as the
  canonical void approach, with `Unit.ValueTask` as the recommended return value.
- **Future reconsideration.** If the .NET runtime introduces a native "void ValueTask"
  optimization that eliminates the `Unit` struct entirely, this decision can be revisited.
  As of .NET 10, no such feature exists or is planned.

---
