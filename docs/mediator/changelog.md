---
layout: default
title: "Changelog"
description: "All notable changes to DSoftStudio.Mediator."
---

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.4] — 2026-03-19

### Fixed

- **CS0436 / CS0121 with `InternalsVisibleTo`** — Source-generated types no longer conflict when a test project (or any referencing project) has `InternalsVisibleTo` access to the host project. ([#1](https://github.com/DSoftStudio/Mediator/issues/1))
  - Generated worker/implementation classes now use the C# 11 `file` modifier, making them invisible across assemblies.
  - Generated extension methods are emitted into per-assembly unique namespaces (`DSoftStudio.Mediator.Generated.{AssemblyName}`) with a `global using` for transparent usage.
- **Cross-project handler discovery** — Handlers defined in referenced projects are now discovered automatically via `[assembly: MediatorHandlerRegistration]` attributes, replacing the previous PE metadata scanning approach that could miss `internal` members.
- **`Send(object)` namespace shadowing** — The runtime `Send(object)` extension is now generated alongside typed extensions in the per-assembly namespace, preventing C# resolution from shadowing typed overloads.

### Changed

- `Send(object)` is now fully source-generated — removed `SenderObjectExtensions.cs` from the runtime DLL.

---

## [1.1.3] — 2026-03-15

### Changed

- **Documentation site** — all doc links now point to [docs.dsoftstudio.com/mediator](https://docs.dsoftstudio.com/mediator) instead of relative GitHub paths.
- **Project website** — NuGet "Project website" updated to `https://docs.dsoftstudio.com/mediator`.

### Fixed

- **SonarCloud quality gate** — excluded `docs/`, `samples/`, and `benchmarks/` from analysis to prevent false-positive bugs on non-production code.

---

## [1.1.2] — 2026-03-15

### Fixed

- **NuGet README rendering** — replaced `<picture>` HTML tag with pure Markdown image syntax (`![alt](url)`) across all package READMEs. NuGet does not support `<picture>` or `<p align="center">` HTML tags, causing raw HTML to render on package pages.

---

## [1.1.1] — 2026-03-15

### Fixed

- **`Send(object)` dispatch fails when multiple `ServiceProvider` instances coexist** —
  The `Send(object)` runtime dispatch delegate referenced the static
  `RequestDispatch<TRequest, TResponse>.Pipeline` field, which is write-once
  (`Interlocked.CompareExchange`). When parallel test classes (or multi-tenant hosts)
  created separate `ServiceProvider` instances with different pipeline configurations,
  the first registration won the static slot. Subsequent providers that lacked
  `PipelineChainHandler` registrations threw `InvalidOperationException`.
  The delegate now resolves directly from the passed-in `IServiceProvider` via
  `GetService<PipelineChainHandler<TRequest, TResponse>>()` (nullable probe) with
  fallback to `GetRequiredService<IRequestHandler<TRequest, TResponse>>()`,
  making it independent of static initialization order.

---

## [1.1.0] — 2026-03-15

### Added

- **Self-handling requests** — request classes (or records) that implement `IRequest<T>`,
  `ICommand<T>`, or `IQuery<T>` and contain a `static Execute` method are automatically
  discovered at compile time and wired into the mediator pipeline. No separate handler
  class is required.

- **Fail-fast handler validation** — new source-generated `ValidateMediatorHandlers()`
  extension method on `IServiceProvider`. Resolves every mediator handler from DI at
  startup and throws an `AggregateException` with all failures if any handler is
  misconfigured.

  ```csharp
  var app = builder.Build();
  app.Services.ValidateMediatorHandlers(); // throws AggregateException if misconfigured
  ```

- **DSOFT002: Duplicate request handler** — compile-time diagnostic (Warning) when
  multiple `IRequestHandler<TRequest, TResponse>` implementations are found for the
  same `<TRequest, TResponse>` pair.

- **DSOFT003: Duplicate stream handler** — compile-time diagnostic (Warning) when
  multiple `IStreamRequestHandler<TRequest, TResponse>` implementations are found for
  the same `<TRequest, TResponse>` pair.

- **Runtime-typed `Send(object)` dispatch** — new `Send(this ISender, object, CancellationToken)`
  extension method for message bus / command queue scenarios where the consumer only has
  an `object` reference at runtime. Uses a compile-time generated
  `FrozenDictionary<Type, DispatchDelegate>` dispatch table — no reflection, no
  `MakeGenericType`, fully AOT-safe.

- **`DSoftStudio.Mediator.OpenTelemetry` package** — automatic distributed tracing and
  metrics for all mediator operations via standard pipeline behaviors.

- **`DSoftStudio.Mediator.FluentValidation` package** — automatic request validation via
  FluentValidation with aggregated error reporting.

- **`DSoftStudio.Mediator.HybridCache` package** — automatic query/request caching via
  Microsoft's `HybridCache` with stampede prevention.

### Changed

- Internal `HandlerInfo` struct refactored to use C# primary constructor (IDE0290).

### Architecture Decisions Recorded

- **ADR-0004:** [Runtime-Typed Send(object) Dispatch](adr/0004-runtime-typed-send.md)
- **ADR-0005:** [OpenTelemetry Instrumentation](adr/0005-opentelemetry-instrumentation.md)

---

## [1.0.6] — 2026-03-12

### Fixed

- **Open-generic pipeline behavior detection** — `MediatorPipelineGenerator` now checks
  `IsGenericTypeDefinition` for pipeline interfaces.
- **`IStreamRequestHandler<TRequest, TResponse>` covariance** — `TResponse` changed from
  invariant to `out` to match the `IStreamRequest<out TResponse>` contract.

### Performance

- **ThreadStatic pipeline chain caches** — eliminates a `GetService` call on the hot path.
- **Handler resolution cache** — replaces `GetRequiredService` on every `Send()` with a
  cached resolution.
- **Pre-linked stream behavior chain** — removes mutable state from the hot path.
- **`SequentialNotificationPublisher` optimized** — materialize handlers to array once;
  index-based loop with `IsCompletedSuccessfully` short-circuit.

### AOT & Trimming

- **Eliminate `MakeGenericType` + `Expression.Compile` from `Publish(object)`** — replaced
  with compile-time generated dispatch table. Fully AOT/trimmer-safe.
- **Mark Abstractions assembly as trimmable/AOT-compatible.**

### Testing

- **Performance regression tests** — allocation and throughput regression tests with
  CI-safe thresholds.

### Benchmarks

- **Added Mediator (martinothamar/Mediator) 3.0.1** to comparison suite.
- **Updated all benchmark results** — Send ~7 ns, Publish ~8.5 ns.

### Documentation

- **Major README rewrite** — 4-way latency/allocation comparison tables, feature
  comparison table, updated messaging.
