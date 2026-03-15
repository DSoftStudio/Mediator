# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] — Unreleased

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
  class is required. The source generator emits an internal adapter that bridges the
  static method to `IRequestHandler<TRequest, TResponse>`, preserving the same
  zero-overhead dispatch path (`HandlerCache`, pipeline behaviors, typed extensions,
  and handler validation).

  Supported return types: `T` (sync), `Task<T>`, `ValueTask<T>`, `void` (Unit),
  `Task` (async Unit).

  DI injection: service parameters in the `Execute` signature are resolved from DI
  automatically. Stateless self-handlers (no DI services) are registered as Singleton;
  with DI dependencies as Transient.

  Full pipeline integration: behaviors, pre/post processors, exception handlers,
  typed `Send()` extensions, and `ValidateMediatorHandlers()` all work with
  self-handling requests.

- **Fail-fast handler validation** — new source-generated `ValidateMediatorHandlers()`
  extension method on `IServiceProvider`. Resolves every mediator handler from DI at
  startup and throws an `AggregateException` with all failures if any handler is
  misconfigured. Detects missing registrations, broken constructor dependencies, and
  incomplete pipeline configurations before the first request is processed.

  ```csharp
  var app = builder.Build();
  app.Services.ValidateMediatorHandlers(); // throws AggregateException if misconfigured
  ```

- **DSOFT002: Duplicate request handler** — compile-time diagnostic (Warning) when
  multiple `IRequestHandler<TRequest, TResponse>` implementations are found for the
  same `<TRequest, TResponse>` pair. With Microsoft.Extensions.DI, only the last
  registration is resolved via `GetRequiredService<T>()` — earlier handlers are
  silently ignored. The diagnostic lists all conflicting implementations.

- **DSOFT003: Duplicate stream handler** — compile-time diagnostic (Warning) when
  multiple `IStreamRequestHandler<TRequest, TResponse>` implementations are found for
  the same `<TRequest, TResponse>` pair. Same root cause as DSOFT002.

- **Runtime-typed `Send(object)` dispatch** — new `Send(this ISender, object, CancellationToken)`
  extension method for message bus / command queue scenarios where the consumer only has
  an `object` reference at runtime. Uses a compile-time generated
  `FrozenDictionary<Type, DispatchDelegate>` dispatch table (same architecture as
  `Publish(object)`) — no reflection, no `MakeGenericType`, fully AOT-safe.

  The extension method design preserves overload resolution: generated typed extensions
  (e.g. `Send(this ISender, Ping)`) are always preferred when the compile-time type is
  known. `Send(object)` is only selected when the argument is typed as `object`.

  Zero impact on the existing `Send<TRequest, TResponse>()` hot path — completely
  separate dispatch table and code path.

  See [ADR-0004](docs/adr/0004-runtime-typed-send.md) for design rationale.

- **`DSoftStudio.Mediator.OpenTelemetry` package** — New companion NuGet package providing
  automatic distributed tracing and metrics for all mediator operations via standard
  `IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>`, and an `INotificationPublisher`
  decorator — zero changes to the core mediator library.

  **Tracing:** Single `ActivitySource("DSoftStudio.Mediator")` with span names following
  `{TypeName} {kind}` convention (e.g. `CreateUser command`, `GetUsers query`).
  Span attributes include `mediator.request.type`, `mediator.response.type`, and
  `mediator.request.kind` (`command`/`query`/`request`/`notification`/`stream`).
  Exception recording with configurable stack traces.

  **Metrics:** Single `Meter("DSoftStudio.Mediator")` with three instruments:
  `mediator.request.duration` (histogram, seconds), `mediator.request.active`
  (up-down counter), `mediator.request.errors` (counter with `error.type` tag).

  **Notification instrumentation:** `InstrumentedNotificationPublisher` decorator
  creates a parent span per `Publish()` call with per-handler child spans — unique
  among .NET mediator libraries.

  **Zero-cost when unused:** `HasListeners()` / `Instrument.Enabled` short-circuits
  add ~1 ns when no OTel exporter is configured.

  **Configuration:** `AddMediatorInstrumentation()` with options for filtering
  (suppress health checks), enrichment (custom tags), and independent tracing/metrics
  toggles.

  See [ADR-0005](docs/adr/0005-opentelemetry-instrumentation.md) for design rationale.

- **`DSoftStudio.Mediator.FluentValidation` package** — New companion NuGet package
  providing automatic request validation via FluentValidation. Registers a single
  open-generic `ValidationBehavior<TRequest, TResponse>` pipeline behavior that
  resolves all `IValidator<TRequest>` instances from DI, runs validation before the
  handler, and throws `MediatorValidationException` on failure.

  **Key features:**
  - Aggregates failures from multiple validators per request type
  - `MediatorValidationException.ErrorsByProperty` for easy `ValidationProblemDetails` mapping
  - Zero-overhead pass-through when no validators are registered for a request type
  - Validators support full DI (constructor injection) — no static registry
  - Single extension method: `services.AddMediatorFluentValidation()`

- **`DSoftStudio.Mediator.HybridCache` package** — New companion NuGet package
  providing automatic query/request caching via Microsoft's `HybridCache`
  (`Microsoft.Extensions.Caching.Hybrid`). Registers a single open-generic
  `CachingBehavior<TRequest, TResponse>` pipeline behavior that checks if the
  request implements `ICachedRequest` and caches results via `HybridCache.GetOrCreateAsync()`.

  **Key features:**
  - Multi-layer caching (L1 in-memory + optional L2 distributed) via `HybridCache`
  - Built-in stampede prevention — concurrent requests for the same key share one execution
  - `ICachedRequest` marker interface with `CacheKey` and `Duration` (default: 60s)
  - Zero-overhead pass-through when the request does not implement `ICachedRequest`
  - Single extension method: `services.AddMediatorHybridCache()`

### Changed

- Internal `HandlerInfo` struct in `DependencyInjectionGenerator` refactored to use
  C# primary constructor (IDE0290).

### Architecture Decisions Recorded

- **ADR-0004: Runtime-Typed Send(object) Dispatch** — Accepted. Adds `Send(object)`
  as an extension method (not interface method) using a compile-time generated
  `FrozenDictionary` dispatch table. Extension method design is required because
  `ISender.Send<TRequest, TResponse>` has two generic type parameters that cannot be
  inferred — an instance `Send(object)` would shadow all generated typed extensions
  due to C# overload resolution rules. See [`docs/adr/0004-runtime-typed-send.md`](docs/adr/0004-runtime-typed-send.md).

- **ADR-0005: OpenTelemetry Instrumentation Package** — Accepted. Separate NuGet
  package (`DSoftStudio.Mediator.OpenTelemetry`) providing automatic distributed
  tracing and metrics via standard pipeline behaviors, with zero impact on the core
  mediator library. See [`docs/adr/0005-opentelemetry-instrumentation.md`](docs/adr/0005-opentelemetry-instrumentation.md).

---

## [1.0.6] - 2026-03-12

### Fixed

- **Open-generic pipeline behavior detection** — `MediatorPipelineGenerator` now checks `IsGenericTypeDefinition` for `IPipelineBehavior<,>`, `IRequestPreProcessor<>`, `IRequestPostProcessor<,>`, and `IRequestExceptionHandler<,>`, fixing a bug where behaviors registered as open generics were silently skipped.
- **`IStreamRequestHandler<TRequest, TResponse>` covariance** — `TResponse` changed from invariant to `out` to match the `IStreamRequest<out TResponse>` contract.

### Performance

- **ThreadStatic pipeline chain caches** — `PipelineChainCache<TRequest, TResponse>` and `StreamPipelineChainCache<TRequest, TResponse>` cache Scoped/Singleton chains per-thread, eliminating a `GetService` call on the hot path. Transient chains continue resolving fresh each call.
- **Handler resolution cache** — `HandlerCache<TRequest, TResponse>` replaces `GetRequiredService` on every `Send()` with a cached resolution.
- **Pre-linked stream behavior chain** — `StreamPipelineChainHandler` now pre-links the behavior chain at construction (like `PipelineChainHandler`), removing mutable state (`_behaviorIndex`, `_active`, `Interlocked`) from the hot path.
- **`SequentialNotificationPublisher` optimized** — Materialize handlers to array once; index-based `for` loop with `IsCompletedSuccessfully` short-circuit; `AwaitRemaining` resumes from `currentIndex + 1` instead of re-scanning with `ReferenceEquals`.
- **`IsPipelineChainCacheable` / `IsStreamChainCacheable`** — New `Volatile.Read`/`Volatile.Write` static flags in `RequestDispatch<T,R>` and `StreamDispatch<T,R>` for zero-cost cache-vs-resolve branching.

### AOT & Trimming

- **Eliminate `MakeGenericType` + `Expression.Compile` from `Publish(object)`** — The `NotificationHandlerWrapper` / `NotificationHandlerWrapperImpl<T>` pattern (runtime reflection) replaced with `NotificationObjectDispatch`, a compile-time generated dispatch table. Fully AOT/trimmer-safe.
- **Delete `NotificationDispatcher`** — Replaced with `NotificationCachedDispatcher` (compile-time dispatch with handler caching).
- **Delete `NotificationHandlerWrapper` / `NotificationHandlerWrapperImpl<T>`** — No longer needed; AOT dispatch table handles all scenarios.
- **Move `IServiceProviderAccessor` from Abstractions to core** — Interceptor-internal interface no longer exposed in the public Abstractions assembly.
- **Mark Abstractions assembly as trimmable/AOT-compatible** — Added `IsTrimmable` and conditional `IsAotCompatible` to the Abstractions csproj.

### Code Quality

- **CA1068** — `CancellationToken` moved to last parameter in `PipelineChainHandler.AwaitPostProcessorAndContinue`, `SequentialNotificationPublisher.AwaitRemaining`, `NotificationCachedDispatcher`.
- **S2699** — Added assertions to `PublishTests` and `NotificationWrapperTests`.
- **CA2211** — Static field visibility fixes.
- **xUnit1031** — Replaced blocking `.Result` calls with `await` in tests.
- **Cognitive complexity** — Extracted `InterceptorHelpers` (shared `ImplementsInterface`, `ResolveRequestParameter`), refactored `ReferencedAssemblyScanner.CollectHandlersFromAssembly`, extracted `TryResolveInferredTypes` in `SendInterceptorGenerator`, extracted `PipelineChainHandler.ComputePipelineMode`.
- **`Unit` operators** — Added `<`, `>`, `<=`, `>=` comparison operators (CA1036).
- **False positive suppressions** — S2326, S2743, S3267.

### Testing

- **Performance regression tests** — `AllocationRegressionTests` and `ThroughputRegressionTests` with CI-safe thresholds (Send ≤ 50 μs, Publish ≤ 50 μs, Stream ≤ 100 μs; Send ≤ 128 B, Publish ≤ 64 B, Stream ≤ 512 B).

### Benchmarks

- **Added Mediator (martinothamar/Mediator) 3.0.1** to comparison suite.
- **Updated all benchmark results** — Send ~7 ns, Publish ~8.5 ns (down from ~18 ns each).
- **Updated `generate-benchmarks-md.ps1`** with isolated vs. combined run variance note.

### Documentation

- **Major README rewrite** — 4-way latency/allocation comparison tables (DSoft, Mediator SG, DispatchR, MediatR), feature comparison table, updated messaging.

### CI/CD

- **SonarCloud workflow** — `.github/workflows/sonar.yml` with Coverlet/OpenCover coverage, sample/benchmark exclusions.

### Stream Pipeline

- **Lifetime-aware stream chain registration** — `StreamGenerator` registers `StreamPipelineChainHandler` as Singleton/Scoped/Transient based on component lifetimes.
- **No-behaviors fast path for streams** — When no stream behaviors are registered, the generated pipeline resolves the handler directly, skipping chain allocation.
