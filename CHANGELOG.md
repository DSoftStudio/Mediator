# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
