# ADR: Handler Discovery and Bug Avoidance in DSoftStudio.Mediator

**Status:** Accepted — Verified against implementation  
**Implementation:** No implementation needed — all claims verified against existing codebase  
**Date:** 2025-07-09

---

## Context

Some mediators (e.g., MediatR, Mediator SG) have known issues in large solutions
or with handler registration/discovery:

- Duplicate handler invocation due to inheritance-based notification dispatch
- Difficulty with internal handler visibility across assemblies
- Lack of dynamic handler addition at runtime
- Potential for duplicate DI registrations when mixing manual and auto-registration

DSoftStudio.Mediator avoids these bugs by using compile-time source generation and
exact-type dispatch. This ADR documents the design decisions and verifies them
against the actual codebase.

---

## Analysis

### 1. Notification Dispatch by Exact Type (Verified ✅)

`NotificationGenerator` groups handlers by exact `TNotification` type at compile time.
The generated `NotificationDispatch<TNotification>.Handlers` is a static generic
specialization — the CLR creates one dispatch table per concrete notification type.

```csharp
// Generated — only handlers for EXACT type MyEvent are included
NotificationDispatch<MyEvent>.TryInitialize(
    new Func<IServiceProvider, INotificationHandler<MyEvent>>[]
    {
        static sp => sp.GetRequiredService<MyEventHandler>(sp),
    });
```

**No inheritance scanning occurs.** If `DerivedEvent : MyEvent`, publishing a
`DerivedEvent` will NOT invoke handlers registered for `MyEvent`. Each notification
type has its own independent dispatch table.

This avoids the MediatR duplicate handler bug where `INotificationHandler<BaseEvent>`
handlers are invoked for all derived event types at runtime via reflection-based
`GetServices<>()`.

> **Design note:** This is intentional. See `copilot-instructions.md`:
> "DSoftStudio.Mediator notification dispatch is by exact type (compile-time),
> not by inheritance hierarchy (runtime reflection)."

### 2. Handler Discovery in Large Solutions (Verified ✅ with corrections)

**Same-project discovery:**

The `DependencyInjectionGenerator` runs as part of the consumer project's compilation.
It scans `ClassDeclarationSyntax` nodes with `BaseList` and discovers ALL handler
classes regardless of access modifier (`public`, `internal`, `protected internal`).
The only exclusions are:

- `abstract` classes (`symbol.IsAbstract`)
- Non-class types (`symbol.TypeKind != TypeKind.Class`)
- `file`-scoped types (`HandlerDiscovery.IsFileLocal()`) — cannot be referenced from
  generated code

**Internal handlers in the same project are always discovered.** No `InternalsVisibleTo`
needed.

**Cross-assembly discovery:**

Handlers in referenced assemblies are discovered via `[assembly: MediatorHandlerRegistration]`
attributes. These attributes are emitted by the upstream project's own generator run.
The downstream `ReferencedAssemblyScanner` reads these attributes and generates DI
registration code that references the handler by fully qualified name.

| Handler accessibility | Same project | Referenced assembly |
|---|---|---|
| `public` | ✅ Discovered | ✅ Discovered via assembly attribute |
| `internal` | ✅ Discovered | ⚠️ Attribute emitted but generated code causes CS0122 unless `InternalsVisibleTo` is set |
| `file` | ❌ Skipped | ❌ Not emitted (filtered by upstream generator) |

**Recommendation for library authors:** Keep handler implementations `public`, or add
`[InternalsVisibleTo]` for the consuming project. Messages (`IRequest<T>`,
`INotification`) should always be `public` — they are the contract.

**Dynamic handler addition at runtime is not supported.** All handlers must be known at
compile time. Dispatch tables use `Interlocked.CompareExchange` for write-once semantics.

### 3. DI Registration and Duplicate Prevention (Verified ✅ with corrections)

The source generator emits explicit DI registrations:

```csharp
// Interface mapping — always added (Add*, not TryAdd*)
AddSingleton<IRequestHandler<Ping, Pong>, PingHandler>(services);

// Concrete type (notification/stream handlers only) — TryAdd* prevents duplicates
TryAddSingleton(services, typeof(MyNotificationHandler));
```

**Key behaviors:**

| Registration type | Method | Duplicate behavior |
|---|---|---|
| Interface mapping (`IRequestHandler<T,R>`) | `AddSingleton/AddTransient` | Multiple registrations possible — last wins for `GetRequiredService` |
| Concrete type (notification/stream) | `TryAddSingleton/TryAddTransient` | First registration wins — subsequent calls are no-op |
| Handler lifetime | Auto-detected | Stateless (no constructor params) → Singleton; with DI deps → Transient |

**Duplicate request/stream handlers are detected at compile time** via DSOFT002 and
DSOFT003 diagnostics (Warning). Multiple `IRequestHandler<T,R>` implementations for
the same `<T,R>` pair trigger a build warning listing all conflicting implementations.

Multiple `INotificationHandler<T>` implementations are expected by design — no
diagnostic is emitted.

> **Note:** The smart handler registration ADR (ADR-smart-handler-registration.md) was
> evaluated and rejected. The duplicate registration problem described in that ADR
> applies to MediatR's runtime assembly scanning architecture, not to DSoftStudio.Mediator's
> compile-time source generation. See that ADR for the full rationale.

### 4. Runtime Validation

`ValidateMediatorHandlers()` provides fail-fast validation at startup. Call after
`BuildServiceProvider()` to resolve every handler from DI and detect:

- Missing handler registrations
- Broken constructor dependencies (unresolvable services)
- Incomplete pipeline configurations

```csharp
var app = builder.Build();
app.Services.ValidateMediatorHandlers(); // throws AggregateException if misconfigured
```

---

## Bug Avoidance Summary

| Bug class | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Duplicate notification invocation (inheritance) | ⚠️ Known issue | ✅ Exact-type dispatch — impossible by design |
| Handler not found (silent failure) | Runtime exception on first request | ✅ DSOFT001 warning at compile time + `ValidateMediatorHandlers()` at startup |
| Duplicate request handler (silent "last wins") | No detection | ✅ DSOFT002 warning at compile time |
| Duplicate stream handler (silent "last wins") | No detection | ✅ DSOFT003 warning at compile time |
| Runtime handler discovery failure | Assembly scanning misses types | ✅ Compile-time discovery — if it builds, it's registered |
| Internal handler not discovered | Depends on scanning config | ✅ Same-project: always discovered. Cross-assembly: `InternalsVisibleTo` or public |
| Handler allocation overhead | Transient per call (always) | ✅ Auto-Singleton for stateless handlers |

---

## Consequences

- Reliable, predictable handler invocation — no inheritance dispatch surprises
- No duplicate notification handler bugs — exact-type dispatch is enforced at compile time
- Large solutions supported — cross-assembly discovery via `[MediatorHandlerRegistration]` assembly attributes
- Compile-time diagnostics (DSOFT001/002/003) catch misconfigurations before runtime
- Runtime validation (`ValidateMediatorHandlers()`) catches DI configuration errors at startup
- No runtime discovery or reflection on the hot path

---
