# ADR: DSoftStudio.Mediator — Architecture Decision Record

## Overview

DSoftStudio.Mediator is an ultra-low-latency mediator for .NET with compile-time dispatch,
zero-allocation pipelines, and a familiar MediatR-style API. This ADR documents all
architectural decisions, design patterns, performance strategies, and trade-offs made
during the development of the library.

---

## 1. Package Architecture

### Decision
Split the library into three internal assemblies shipped as a single NuGet package:

| Assembly | Target | Role |
|----------|--------|------|
| `DSoftStudio.Mediator.Abstractions` | .NET Standard 2.0 + .NET 8 | Public contracts (`IRequest`, `INotification`, `IPipelineBehavior`, etc.) |
| `DSoftStudio.Mediator` | .NET 8 | Runtime (mediator, caches, dispatch tables, publishers) |
| `DSoftStudio.Mediator.Generators` | .NET Standard 2.0 | Roslyn 4.12 incremental source generators |

### Rationale
- Abstractions target .NET Standard 2.0 for maximum compatibility (shared libraries, .NET Framework consumers).
- Runtime targets .NET 8 for modern APIs (`FrozenDictionary`, `ArgumentNullException.ThrowIfNull`, primary constructors).
- Generators target .NET Standard 2.0 (Roslyn analyzer/generator host requirement).
- All three are shipped embedded in a single `DSoftStudio.Mediator` NuGet package — Abstractions and Generators are not published separately.

### Consequences
- Consumers add a single package reference.
- Abstractions can be referenced from .NET Standard 2.0 libraries without pulling in the runtime.

---

## 2. Compile-Time Dispatch via Source Generators

### Decision
Use Roslyn incremental source generators to discover handlers, emit dispatch tables,
and intercept `Send()`/`Publish()`/`CreateStream()` call sites at compile time.

### Generators

| Generator | Purpose |
|-----------|---------|
| `SendInterceptorGenerator` | Intercepts `ISender.Send<TRequest, TResponse>()` call sites and replaces them with direct pipeline invocation via C# interceptors |
| `PublishInterceptorGenerator` | Intercepts `IPublisher.Publish<TNotification>()` call sites |
| `StreamInterceptorGenerator` | Intercepts `ISender.CreateStream<TRequest, TResponse>()` call sites |
| `MediatorPipelineGenerator` | Discovers all `IRequestHandler<,>` implementations, generates `MediatorRegistry` for pipeline precompilation |
| `NotificationGenerator` | Discovers all `INotificationHandler<>` implementations, generates `NotificationRegistry` |
| `StreamGenerator` | Discovers all `IStreamRequestHandler<,>` implementations, generates `StreamRegistry` |
| `DependencyInjectionGenerator` | Generates `RegisterMediatorHandlers()` extension method with auto-lifetime detection |
| `MediatorExtensionsGenerator` | Generates typed extension methods for `Send`, `Publish`, `CreateStream` |
| `HandlerDiscovery` | Shared helper for extracting handler type information from symbols |
| `ReferencedAssemblyScanner` | Scans referenced assemblies for handler registrations (cross-project support) |
| `InterceptorHelpers` | Shared helpers (`ImplementsInterface`, `ResolveRequestParameter`) |

### Rationale
- Compile-time dispatch eliminates virtual dispatch overhead, dictionary lookups, and reflection at runtime.
- Interceptors replace the `Mediator.Send()` method frame entirely — the JIT sees a direct call to the pipeline.
- Incremental generators ensure fast IDE responsiveness and build times.

### Consequences
- All handlers must be discoverable at compile time.
- No runtime handler addition or dynamic dispatch (by design).
- IDE must support Roslyn 4.12+ for interceptor features.

---

## 3. Static-Generic Dispatch Tables

### Decision
Use static generic classes (`RequestDispatch<TRequest, TResponse>`, `NotificationDispatch<TNotification>`,
`StreamDispatch<TRequest, TResponse>`) as write-once dispatch tables.

### How it works
- The CLR creates one specialization per closed generic type → O(1) lookup via static field read.
- Populated once at startup by source-generated code.
- `Interlocked.CompareExchange` enforces write-once semantics.
- `Volatile.Read`/`Volatile.Write` for flags (`HasPipelineChain`, `IsPipelineChainCacheable`).

### Rationale
- Single static field load per dispatch — faster than any dictionary or concurrent collection.
- No allocation, no locking on the hot path.
- Thread-safe initialization.

### Consequences
- Dispatch tables are immutable after startup.
- Cannot be modified at runtime (no dynamic handler addition).

---

## 4. Zero-Allocation Pipeline via Interface Dispatch

### Decision
`PipelineChainHandler<TRequest, TResponse>` passes `this` as the `next` parameter to each
behavior via `IRequestHandler<TRequest, TResponse>` interface dispatch (virtual call),
instead of using delegate closures.

### How it works
- Each behavior receives `this` (the chain handler) as `next`.
- Calling `next.Handle(request, ct)` is a virtual call (~0.5 ns) instead of a delegate invocation (~2 ns).
- No closures, no delegate allocations, no `Func<>` wrapping.
- Pre-linked chain built at construction time for streams (`StreamPipelineChainHandler`).

### Allocation profile
- **72 B constant** per `Send()` regardless of pipeline depth (0, 1, 3, 5 behaviors).
- **0 B** per `Publish()`.
- **232 B** per `CreateStream()`.

### Rationale
- Interface dispatch is the cheapest possible abstraction in the CLR for behavior chaining.
- Constant allocation eliminates GC pressure scaling with pipeline depth.

### Consequences
- Pipeline depth has minimal latency impact (~2 ns per behavior).
- Reentrancy detection (`_active` flag) falls back to closure-based chain (correct but allocating).

---

## 5. ThreadStatic Caching Strategy

### Decision
Use `[ThreadStatic]` fields to cache handler and pipeline chain resolutions per thread.

### Cache types

| Cache | Purpose | Hit cost | Miss cost |
|-------|---------|----------|-----------|
| `HandlerCache<TRequest, TResponse>` | Caches `IRequestHandler` on no-behaviors path | ~1 ns | ~10 ns (DI resolve) |
| `PipelineChainCache<TRequest, TResponse>` | Caches `PipelineChainHandler` on behaviors path | ~1 ns | ~10 ns (DI resolve) |
| `StreamPipelineChainCache<TRequest, TResponse>` | Caches `StreamPipelineChainHandler` | ~1 ns | ~10 ns (DI resolve) |
| `StreamHandlerCache<TRequest, TResponse>` | Caches `IStreamRequestHandler` | ~1 ns | ~10 ns (DI resolve) |
| `NotificationHandlerCache<TNotification>` | Caches resolved notification handler arrays | ~1 ns | ~10 ns (DI resolve) |

### Guard mechanism
- `ReferenceEquals(_cachedProvider, serviceProvider)` detects scope changes.
- On scope change (e.g., new ASP.NET Core request) → cache miss → fresh DI resolve.
- After `async/await` thread hop → cache miss → one DI lookup on new thread.

### Rationale
- Eliminates ~10 ns `GetRequiredService` call on every `Send()` for Scoped/Singleton handlers.
- `[ThreadStatic]` is lock-free, zero-allocation, and CPU cache friendly.
- Only used when `IsPipelineChainCacheable` is true (Scoped/Singleton). Transient chains always resolve fresh.

### Consequences
- Thread hops after `await` cause a single cache miss (no correctness issue).
- Memory overhead: one cached reference per thread per handler type.

---

## 6. Notification Dispatch by Exact Type

### Decision
Notification dispatch is by **exact compile-time type**, not by inheritance hierarchy.

### How it works
- `Publish<TNotification>()` dispatches only to handlers registered for the exact `TNotification` type.
- No base class or interface handler matching.
- No runtime type scanning or reflection.

### Rationale
- Avoids the MediatR/Mediator SG bug where base class handlers are invoked for derived notifications, causing duplicate handler execution.
- Compile-time type resolution is faster and more predictable.
- Documented as a deliberate design decision.

### Consequences
- If you need a handler to respond to multiple notification types, register it explicitly for each type.
- No "catch-all" base class handlers.

---

## 7. AOT and Trimming Compatibility

### Decision
The library is fully compatible with .NET Native AOT publishing and IL trimming.

### Implementation
- Both packages ship with `IsAotCompatible` and `IsTrimmable` enabled.
- `EnableTrimAnalyzer` is active at build time.
- Hot path uses no reflection, no `MakeGenericType`, no `Expression.Compile`, no dynamic method generation.
- `Publish(object)` overload uses `NotificationObjectDispatch` — a compile-time generated `FrozenDictionary<Type, DispatchDelegate>` dispatch table populated by the source generator. No `MakeGenericType` at runtime.
- Deleted legacy `NotificationDispatcher`, `NotificationHandlerWrapper`, `NotificationHandlerWrapperImpl<T>` (reflection-based).
- `IServiceProviderAccessor` moved from Abstractions (public) to core (internal).

### Rationale
- Native AOT and trimming are increasingly important for serverless, containerized, and edge workloads.
- Reflection-based dispatch is incompatible with AOT and produces trim warnings.

### Consequences
- All dispatch is resolved at compile time.
- No fallback to reflection-based dispatch.

---

## 8. Auto-Singleton Handler Registration

### Decision
Stateless handlers (no constructor parameters) are automatically registered as Singleton.
Handlers with DI dependencies are registered as Transient.

### Rationale
- Singleton registration eliminates per-call allocation for stateless handlers.
- Transient is the safe default for handlers that inject scoped or transient services.
- Users can override lifetimes after `RegisterMediatorHandlers()` and before `PrecompilePipelines()`.

### Consequences
- Zero per-call allocation for stateless handlers.
- Users must place lifetime overrides before `PrecompilePipelines()`.

---

## 9. Pipeline Lifetime Determination

### Decision
`PrecompilePipelines()` determines each `PipelineChainHandler` lifetime based on the registered components:

| Components | Chain Lifetime |
|------------|---------------|
| All Singleton | Singleton |
| Any Scoped | Scoped |
| Any Transient | Transient |

### Rationale
- Ensures correct DI semantics without manual configuration.
- Singleton chains are cached per-thread for maximum performance.

### Consequences
- Registrations added after `PrecompilePipelines()` are not picked up.
- Registration order matters (documented in README).

---

## 10. Registration Order Contract

### Decision
All service registrations must happen before the corresponding `Precompile*` call.

### Required order
```
AddMediator()                → Core mediator services
RegisterMediatorHandlers()   → Source-generated handler registrations
[Register behaviors, processors, exception handlers, lifetime overrides]
PrecompilePipelines()        → Scans for IPipelineBehavior, Pre/Post processors, exception handlers
PrecompileNotifications()    → Builds static dispatch arrays for each INotification type
PrecompileStreams()           → Builds static factory delegates for each IStreamRequest type
```

### Rationale
- Precompilation inspects the `IServiceCollection` snapshot at that point in time.
- Avoids runtime discovery or lazy initialization overhead.

### Consequences
- Registrations after `Precompile*` calls are silently ignored.
- Documented in README with clear examples.

---

## 11. Sync Fast-Path Optimization

### Decision
All dispatch methods check `IsCompletedSuccessfully` before entering the async state machine.

### How it works
- If the handler completes synchronously (common for in-memory handlers), the `ValueTask`/`Task` is returned directly.
- No async state machine allocation.
- `SequentialNotificationPublisher` and `NotificationCachedDispatcher` use index-based `for` loops with `IsCompletedSuccessfully` short-circuit.

### Rationale
- Most in-process handlers complete synchronously.
- Avoiding the async state machine saves ~40–80 B per call.

### Consequences
- Async handlers still work correctly — the async path is entered only when needed.

---

## 12. Open-Generic Pipeline Behavior Support

### Decision
Open-generic registrations (`typeof(IPipelineBehavior<,>)`, `typeof(IRequestPreProcessor<>)`,
`typeof(IRequestPostProcessor<,>)`, `typeof(IRequestExceptionHandler<,>)`) are detected
at pipeline precompilation time via `IsGenericTypeDefinition`.

### Rationale
- Users register behaviors as open generics (standard DI pattern).
- The generator must detect these at precompilation to include them in the pipeline chain.
- Fixed in v1.0.6 — previously, open-generic behaviors were silently skipped.

### Consequences
- Both closed and open-generic behaviors are supported.
- No performance impact — detection happens at startup only.

---

## 13. CQRS Support

### Decision
Provide `ICommand<TResponse>`, `IQuery<TResponse>`, `ICommandHandler`, and `IQueryHandler`
as semantic aliases for `IRequest<TResponse>` and `IRequestHandler<TRequest, TResponse>`.

### Rationale
- Enables clean CQRS separation without additional infrastructure.
- Non-generic marker interfaces (`ICommand`, `IQuery`) allow runtime behavior targeting (e.g., transactions for commands, caching for queries).

### Consequences
- Commands and queries flow through the same pipeline.
- Pipeline behaviors can use `is ICommand` / `is IQuery` for targeting.

---

## 14. Pluggable Notification Strategies

### Decision
Notification dispatch strategy is configurable via `INotificationPublisher`.

### Built-in strategies

| Strategy | Behavior |
|----------|----------|
| Sequential (default) | Handlers run one at a time in registration order. If one throws, the rest are skipped. |
| `ParallelNotificationPublisher` | All handlers start concurrently via `Task.WhenAll`. If any throw, `AggregateException` is raised after all complete. |

### Custom strategies
Users can implement `INotificationPublisher` for fire-and-forget, batched, prioritized, etc.

### Rationale
- Different applications have different reliability/latency requirements for notifications.
- Decouples notification dispatch from the mediator core.

### Consequences
- Default (sequential) is the safest option.
- Custom publishers must handle exceptions and cancellation correctly.

---

## 15. Multi-Project Support

### Decision
Reference the source generator package **only in the host project**. Feature modules
reference only the abstractions package.

### Requirements
- All handlers/messages must be visible to the project that references the source generator.
- Internal handlers require `InternalsVisibleTo` or same-assembly placement.
- Only one `IMediator` registration in the DI container.
- `AddMediator()` called only once, in the host project.

### Rationale
- Avoids duplicate `IMediator` implementations and conflicting dispatch tables.
- `ReferencedAssemblyScanner` discovers handlers in referenced assemblies.

### Consequences
- Correct project structure is required for handler discovery.
- Documented in README and ADR.

---

## 16. Performance Benchmarks (.NET 10, Isolated Runs)

### Send (No Behaviors)
| Method     | Latency  | Alloc | Ratio |
|------------|----------|-------|-------|
| DirectCall | 7.511 ns | 72 B  | 1.00× |
| DSoft_Send | 8.039 ns | 72 B  | 1.07× |

### Send (Behaviors)
| Method                | Latency   | Alloc | Ratio |
|-----------------------|-----------|-------|-------|
| DirectCall            |  6.646 ns | 72 B  | 1.00× |
| DSoft_Send            |  7.886 ns | 72 B  | 1.19× |
| DSoft_Send_3Behaviors | 15.306 ns | 72 B  | 2.30× |
| DSoft_Send_5Behaviors | 16.544 ns | 72 B  | 2.49× |

### Cross-Library Comparison (All Libraries, Combined Run)
| Operation        | DSoft    | Mediator SG | DispatchR  | MediatR    |
|------------------|----------|-------------|------------|------------|
| Send()           | 7.1 ns   | 12.5 ns     | 33.4 ns    | 42.1 ns    |
| Send() 5 beh     | 15.5 ns  | 21.2 ns     | 53.5 ns    | 150.2 ns   |
| Publish()        | 8.5 ns   | 10.2 ns     | 35.0 ns    | 136.1 ns   |
| CreateStream()   | 45.8 ns  | 45.3 ns     | 67.1 ns    | 124.2 ns   |
| Cold Start       | 1.63 µs  | 7.41 µs     | 1.91 µs    | 3.10 µs    |

### Allocation Comparison
| Operation        | DSoft | Mediator SG | DispatchR | MediatR |
|------------------|-------|-------------|-----------|---------|
| Send()           | 72 B  | 72 B        | 72 B      | 272 B   |
| Send() 5 beh     | 72 B  | 72 B        | 72 B      | 1,088 B |
| Publish()        | 0 B   | 0 B         | 0 B       | 768 B   |
| CreateStream()   | 232 B | 232 B       | 232 B     | 624 B   |

---

## 17. Code Quality & CI

### Decision
Maintain strict code quality via SonarCloud, Roslyn analyzers, and performance regression tests.

### Measures
- SonarCloud CI workflow with Coverlet/OpenCover coverage (94.9%).
- Performance regression tests with CI-safe thresholds:
  - Send ≤ 50 µs, Publish ≤ 50 µs, Stream ≤ 100 µs
  - Send ≤ 128 B, Publish ≤ 64 B, Stream ≤ 512 B
- Fixed CA1068, S2699, CA2211, xUnit1031, CA1036.
- Reduced cognitive complexity across generators.
- `Unit` operators (`<`, `>`, `<=`, `>=`) for CA1036 compliance.

### Consequences
- Consistent code quality across contributions.
- Performance regressions are caught automatically in CI.

---

## Document History

| Date       | Version | Author      | Changes |
|------------|---------|-------------|---------|
| 2026-03-12 | 1.0.6   | DSoftStudio | Initial ADR covering all architectural decisions |

---
