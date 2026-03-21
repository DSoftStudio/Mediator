---
layout: default
title: "Changelog - DSoftStudio.Mediator"
description: "All notable changes to DSoftStudio.Mediator."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[&larr; Back to Documentation](index.md)

# Changelog

## [1.1.6] — 2026-03-21

### Added

- **Native AOT safety integration tests** — New `NativeAotSafetyTests` suite (7 tests) validating that `PrecompilePipelines()` correctly replaces open-generic `IPipelineBehavior<,>` descriptors with closed-generic versions. Covers end-to-end dispatch (Unit/int/bool), multiple behaviors ordering, zero open-generic assertion, lifetime preservation (Transient/Scoped/Singleton), and idempotency.

### Fixed

- **CS8600 nullable conversion in `MockDetectionAnalyzer`** — The `DetectMockingLibrary` call site now declares `string?` and uses `is null` pattern matching, eliminating the CS8600 warning without sentinel values.
- **CS8603 possible null return in `MockDetectionAnalyzer`** — `DetectMockingLibrary` return type corrected to `string?` to match its nullable contract.
- **CS8625 null-to-non-nullable in `ReferencedAssemblyScanner`** — `GetAllExternalHandlers` parameter changed to `List<SkippedHandlerInfo>?` to accurately reflect its optional nature.
- **CS8765 nullable parameter mismatch in `MockDetectionAnalyzerTests`** — `TryGetValue` overrides now match the base `AnalyzerConfigOptions` signature (`out string value`) using `null!` for the false-return pattern.
- **xUnit1051 across all test projects** — All `CancellationToken.None` / omitted `CancellationToken` arguments in test methods replaced with `TestContext.Current.CancellationToken` for responsive test cancellation under xUnit v3.

### Changed

- **README rewrite** — Complete restructure with new sections: Execution Model (ASCII pipeline diagram), When to Use This (explicit DSoft vs MediatR positioning), Ecosystem (category-labeled companion packages). Feature Comparison table empirically verified against martinothamar/Mediator 3.0.1, DispatchR 2.1.1, and MediatR 12.4.1 with live test projects. DispatchR corrected to ✅ for exact-type notification dispatch. Mediator (SG) Native AOT compatibility confirmed via `dotnet publish` AOT + native binary execution.
- **Design-time build cache cleanup** — Stale `.dtbcache.v2` causing IntelliSense false positives (CS0246/CS0518) documented and resolved via cache invalidation.
- **Companion packages bumped to 1.0.4** — `DSoftStudio.Mediator.FluentValidation`, `DSoftStudio.Mediator.HybridCache`, `DSoftStudio.Mediator.OpenTelemetry` updated to depend on `DSoftStudio.Mediator >= 1.1.6`.

---

## [1.1.5] — 2026-03-20

### Added

- **`DSoftStudio.Mediator.Abstractions` NuGet package** — Contracts (interfaces and base types) are now published as a separate package. Domain, application-core, and test projects can reference only `DSoftStudio.Mediator.Abstractions` to get `ISender`, `IPublisher`, `IMediator`, `IRequest<T>`, `INotification`, and related abstractions **without** pulling in the runtime or source generators. This is the recommended pattern for unit-testing with mocking frameworks (Moq, NSubstitute, etc.) since no interceptors are active.
- **DSOFT004: Mock detection analyzer** — New compile-time diagnostic (Warning) that detects when a project references both the source generators and a mocking framework (Moq, NSubstitute, FakeItEasy). Recommends referencing only `DSoftStudio.Mediator.Abstractions` in test projects for clean mock isolation.
- **`DSoftMediatorSuppressInterceptors` MSBuild kill switch** — Set `<DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors>` in a project to completely disable interceptor generation. Useful for test projects or environments where interceptors are undesirable.
- **`NotificationPublisherFlag`** — Write-once volatile flag that allows the runtime `Publish` path to skip unnecessary overhead when no notification publishers are registered.
- **Cross-project mocking sample** — New `samples/cross-project-mocking/` demonstrates the recommended architecture: `Host` (runtime + generators), `Host.Application` (abstractions only), `Host.Tests` (mocks against abstractions).
- **DSOFT005: Internal handler skipped analyzer** — New compile-time diagnostic (Warning) reported for every handler discovered in a referenced assembly but skipped because it is not accessible. The message includes the handler type name and the assembly it belongs to, so library authors can decide whether to make the handler `public` or add an `InternalsVisibleTo` attribute.
- **CS0122 regression tests** — New test project (`DSoftStudio.Mediator.ModularMonolith.Tests`) with compile-time guard (`WarningsAsErrors=CS0122`) ensuring the internal-handler accessibility fix cannot regress.

### Fixed

- **DSOFT004 not respected in transitive projects** — `DSoftMediatorSuppressInterceptors=true` was silently ignored when the project consumed DSoftStudio.Mediator transitively (e.g. `Host.Tests` ? `Host` via `ProjectReference`). Root cause: the `CompilerVisibleProperty` declaration lived only in the `build/` NuGet folder, which is **not** transitive. Added a `buildTransitive/DSoftStudio.Mediator.props` file containing just the `CompilerVisibleProperty` declaration so the source generator can read the suppress flag in any downstream project. Interceptor namespaces are intentionally **not** included in the transitive props.
- **Interceptors rewriting call sites inside expression tree lambdas** — The source generators (`SendInterceptorGenerator`, `PublishInterceptorGenerator`, `StreamInterceptorGenerator`) no longer rewrite `Send`, `Publish`, or `CreateStream` calls that appear inside expression tree lambdas (e.g. Moq `Setup()` / `Verify()`). A new `IsInsideExpressionTreeLambda` helper walks the syntax tree and checks the lambda's `ConvertedType` against `System.Linq.Expressions.Expression<T>`, skipping those call sites. Direct invocations outside expression trees continue to be intercepted as before. ([#2](https://github.com/DSoftStudio/Mediator/issues/2))
- **Flaky parallel notification tests** — `ParallelNotificationPublisherTests` and `PipelineGcLeakTests` stabilized with deterministic synchronization to eliminate intermittent CI failures.
- **Modular monolith CS0122** — The source generator no longer emits DI registrations for `internal` handlers discovered in referenced assemblies, which previously caused `CS0122` at compile time. Accessibility is now validated during generation, and `InternalsVisibleTo` is respected. A new **DSOFT005** warning identifies every skipped handler.

### Changed

- **Branchless interceptor dispatch (Release builds)** — Interceptor generators now emit `OptimizationLevel`-conditional code: Release builds use `castclass` (branchless, ~0.36 ns saving via GDV), Debug builds use `isinst` + null-check for mock-framework compatibility. Hot-path `Send` is now **1.05×** vs raw handler.
- **Publish optimization** — `Publish` dispatch leverages `NotificationPublisherFlag` to skip publisher resolution when none are registered. Overhead dropped from **2.19×** to **1.07–1.11×** vs raw handler.
- **NuGet package split** — `DSoftStudio.Mediator` now declares a public NuGet dependency on `DSoftStudio.Mediator.Abstractions` (previously the Abstractions DLL was embedded with `PrivateAssets="all"`). Companion packages (`FluentValidation`, `HybridCache`, `OpenTelemetry`) receive `Abstractions` transitively through `DSoftStudio.Mediator`.
- **CI workflow** — Build & Test and Publish are now separate jobs. The `publish` job uses `needs: build-and-test`, guaranteeing that **no package is published if any test fails**. GitHub Packages receives packages on every push to `main`; NuGet.org only on version tags (`v*`).
- **Companion packages bumped to 1.0.3-rc.2** — `DSoftStudio.Mediator.FluentValidation` (FluentValidation 12.1.1), `DSoftStudio.Mediator.HybridCache` (HybridCache 10.4.0), `DSoftStudio.Mediator.OpenTelemetry` (OpenTelemetry 1.15.0) updated with latest dependency versions.
- **Abstractions simplified to `netstandard2.0` only** — Removed `net8.0` multi-target; `netstandard2.0` provides maximum compatibility across all .NET versions.
- **Test infrastructure migrated to xunit v3** — All 7 test projects updated from xunit v2 to xunit v3 (3.2.2) with `xunit.runner.visualstudio` 3.1.5 and `Microsoft.NET.Test.Sdk` 18.3.0.

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

  See [ADR-0004](docs/mediator/adr/0004-runtime-typed-send.md) for design rationale.

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

  See [ADR-0005](docs/mediator/adr/0005-opentelemetry-instrumentation.md) for design rationale.

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
  due to C# overload resolution rules. See [`docs/mediator/adr/0004-runtime-typed-send.md`](docs/mediator/adr/0004-runtime-typed-send.md).

- **ADR-0005: OpenTelemetry Instrumentation Package** — Accepted. Separate NuGet
  package (`DSoftStudio.Mediator.OpenTelemetry`) providing automatic distributed
  tracing and metrics via standard pipeline behaviors, with zero impact on the core
  mediator library. See [`docs/mediator/adr/0005-opentelemetry-instrumentation.md`](docs/mediator/adr/0005-opentelemetry-instrumentation.md).

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

- **Performance regression tests** — `AllocationRegressionTests` and `ThroughputRegressionTests` with CI-safe thresholds (Send = 50 µs, Publish = 50 µs, Stream = 100 µs; Send = 128 B, Publish = 64 B, Stream = 512 B).

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
