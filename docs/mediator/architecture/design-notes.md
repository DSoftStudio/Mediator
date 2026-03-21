---
layout: default
title: "Design Notes - DSoftStudio.Mediator"
description: "Interceptor generation, mock safety, suppression, and notification dispatch design decisions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[&larr; Back to Documentation](../index.md)

# Design Notes

## Interceptor code generation (Release vs Debug)

The source generators emit C# [interceptors](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12#interceptors) that replace `ISender.Send`, `IPublisher.Publish`, and `IMediator.CreateStream` call sites at compile time with direct pipeline invocations — eliminating virtual dispatch entirely.

The generated code adapts to the build's **`OptimizationLevel`**:

| Build | Generated pattern | Overhead vs direct call | Mock-safe |
|---|---|---|---|
| **Release** | Branchless `castclass IServiceProviderAccessor` | ~0.6 ns | ❌ throws `InvalidCastException` on mocks |
| **Debug** | `is not IServiceProviderAccessor` + virtual fallback | ~3 ns | ✅ graceful fallback to virtual dispatch |

**Why?** A single `isinst` + branch instruction prevents the JIT's Guarded Devirtualization (GDV) from fully devirtualizing the interface call, adding ~3 ns on every invocation. The branchless `castclass` pattern in Release lets GDV optimize the dispatch to a method-table pointer comparison + direct field load — effectively zero overhead.

In **Debug** builds (where tests typically run), the generated interceptors detect test doubles (Moq, NSubstitute, etc.) that don't implement `IServiceProviderAccessor` and fall back to virtual dispatch, so mocked `ISender`/`IMediator` instances work as expected.

> **Tip:** If you run tests in Release mode and mock `ISender` in a project that references the generator, the interceptor will throw `InvalidCastException`. Either remove the generator reference from pure unit-test projects or use the strongly-typed mock setup. See the **Suppressing interceptors** section below.

---

## Suppressing interceptors (`DSoftMediatorSuppressInterceptors`)

If your test project references `DSoftStudio.Mediator` directly (not just `Abstractions`) **and** mocks `ISender`/`IPublisher`/`IMediator`, you can disable interceptor generation entirely with an MSBuild property:

```xml
<PropertyGroup>
  <DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors>
</PropertyGroup>
```

When set to `true`:
- The `.props` auto-import stops adding the interceptor namespace, so the C# compiler will not recognize `[InterceptsLocation]` attributes.
- All three source generators (`Send`, `Publish`, `Stream`) read the property via `AnalyzerConfigOptionsProvider` and skip code emission entirely.
- Your project falls back to standard virtual dispatch through the `IMediator` / `ISender` interfaces — fully mock-safe at any optimization level.

---

## DSOFT004 — Mocking library detected with interceptors enabled

The generator package includes an incremental analyzer that scans `ReferencedAssemblyNames` for well-known mocking libraries:

> Moq · NSubstitute · FakeItEasy · Telerik.JustMock · RhinoMocks · NimbleMocks

If any of these are referenced **and** `DSoftMediatorSuppressInterceptors` is not `true`, the build emits:

```
warning DSOFT004: This project references mocking library 'Moq' and has interceptors enabled.
In Release builds, interceptors use a branchless cast that throws InvalidCastException on mock
objects. Either reference only DSoftStudio.Mediator.Abstractions in test projects, or set
<DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors> in this project.
```

**Recommended patterns:**

| Test project setup | Interceptors | Mock-safe | Action needed |
|---|---|---|---|
| References only `Abstractions` | Not generated | ✅ | None (preferred) |
| References `Mediator` + `SuppressInterceptors=true` | Suppressed | ✅ | Add MSBuild property |
| References `Mediator` + Debug build | Generated (isinst fallback) | ✅ | None |
| References `Mediator` + Release build | Generated (castclass) | ❌ | Suppress or restructure |

---

## Abstractions-only project pattern (recommended)

The recommended multi-project architecture separates the generator host from your domain/application layer:

```
Host (Web API / Worker)
├── References: DSoftStudio.Mediator (includes source generators)
├── References: Host.Application
│
Host.Application (Domain / Application layer)
├── References: DSoftStudio.Mediator.Abstractions (interfaces only)
├── Contains: IRequest<T> commands, IRequestHandler<,> implementations
│
Host.Tests (Unit tests)
├── References: DSoftStudio.Mediator.Abstractions (interfaces only)
├── Mocks: ISender / IPublisher via Moq, NSubstitute, etc.
```

**Why this works:**

1. **Source generators run in `Host`** — they discover handlers from `Host.Application` via the `ReferencedAssemblyScanner` Phase 2 (type-based fallback), which walks all exported types in assemblies that reference Abstractions.
2. **Typed extensions are generated** — e.g. `sender.Send(new RunTaskCommand())` compiles to `sender.Send<RunTaskCommand, int>(request, ct)` via pure virtual dispatch. No `IServiceProviderAccessor` cast.
3. **Test projects are fully mock-safe** — since they only reference `Abstractions`, no interceptors are generated. Mocking `ISender` works with any test double framework.

```csharp
// Host.Application — only references Abstractions
public sealed record RunTaskCommand(string Name) : IRequest<int>;

public sealed class RunTaskCommandHandler : IRequestHandler<RunTaskCommand, int>
{
    public ValueTask<int> Handle(RunTaskCommand request, CancellationToken ct) => new(42);
}

// Host.Tests — mocks ISender freely, no generator interference
var sender = new Mock<ISender>();
sender.Setup(s => s.Send<RunTaskCommand, int>(It.IsAny<RunTaskCommand>(), default))
      .ReturnsAsync(42);
```

> **Note:** The `Send(object)` runtime dispatch overload requires the real `Mediator` instance (it needs `IServiceProviderAccessor`). If you call `sender.Send((object)request)` on a mock, you'll get a helpful `InvalidOperationException` guiding you to use the typed overload instead.

---

## Notification dispatch by exact type

Notification handlers are dispatched by **exact compile-time type**, not by runtime inheritance hierarchy. This is a deliberate design decision:

```csharp
// Only handlers registered for OrderPlaced are invoked —
// handlers for INotification or a base class are NOT invoked.
await mediator.Publish(new OrderPlaced(orderId));
```

This avoids the [MediatR duplicate-handler bug](https://github.com/jbogard/MediatR/issues) where a single notification can trigger base-class handlers unexpectedly, and enables the source generator to emit a static dispatch table at build time with zero reflection.

---

## Publish interceptor — `NotificationPublisherFlag` bypass

Most applications never register a custom `INotificationPublisher`. Without optimization, every generated `Publish` interceptor would call `GetService<INotificationPublisher>()` on every invocation — even when the result is always `null`. That DI probe alone costs ~3–4 ns per call.

`NotificationPublisherFlag` is a write-once global `Volatile` boolean that eliminates this probe:

1. **Default path (no custom publisher):** The flag is `false`. The generated interceptor reads `HasCustomPublisher` (~0.1 ns) and short-circuits directly to `NotificationCachedDispatcher.DispatchSequential` — zero DI lookup.
2. **Custom publisher path:** When `INotificationPublisher` is registered (e.g. `ParallelNotificationPublisher`, OpenTelemetry's `InstrumentedNotificationPublisher`), the `Mediator` constructor calls `MarkRegistered()` once. All subsequent interceptors see the flag and take the `GetService` path as before.

| Scenario | Before optimization | After optimization |
|---|---|---|
| No custom publisher (default) | `GetService` returns `null` (~3–4 ns) | `Volatile.Read` (~0.1 ns) |
| Custom publisher registered | `GetService` returns instance (~3–4 ns) | `Volatile.Read` + `GetService` (~3–4 ns) |

Net effect: **~4 ns saved per `Publish` call** in the default (no custom publisher) path — bringing the Publish interceptor from ~2.2× to ~1.1× overhead vs direct dispatch.
