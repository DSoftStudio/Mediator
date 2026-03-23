---
layout: default
title: "Production Validation - DSoftStudio.Mediator"
description: "Enterprise integration test suite: failure injection, chaos, concurrency, and allocation regression testing."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[&larr; Back to Documentation](../index.md)

# Production Validation

Most mediator libraries ship with unit tests that verify correctness under ideal conditions — single-threaded, synchronous, no failures. DSoftStudio.Mediator is validated under conditions that reflect real production systems: concurrency, failure, chaos, and resource pressure.

> This mediator is validated under concurrency, failure, and chaos — not just benchmarks.

This page documents the enterprise integration test suite, what each category measures, and why it matters.

---

## Mental model

These tests are designed to answer one question:

**What happens when the system is under real pressure?**

Not:
- Single-threaded execution
- Ideal conditions

But:
- Concurrency
- Failures
- Unpredictable timing

---

## Test suite overview

**48 tests** across **14 categories**, covering scenarios that most mediator libraries do not test.

| Category | Tests | What it validates |
|---|---:|---|
| [Multi-project discovery](#1-multi-project-discovery) | 4 | Cross-assembly handler resolution with source generators |
| [Dependency injection lifetimes](#2-dependency-injection-lifetimes) | 4 | Scoped/Transient/Singleton correctness through the pipeline |
| [Deep pipeline (6 behaviors)](#3-deep-pipeline) | 4 | Behavior ordering, exception propagation, retry, exception handlers |
| [Concurrency](#4-concurrency) | 3 | 2000+ parallel Send/Publish with result verification |
| [Native AOT safety](#5-native-aot-safety) | 3 | PrecompilePipelines removes open-generics, idempotent |
| [Expression tree safety](#6-expression-tree-safety) | 2 | Moq/NSubstitute compatibility — no interceptor rewrite |
| [Complex generics](#7-complex-generics) | 5 | Nullable reference/value types, stream with value-type response |
| [Runtime vs compile-time dispatch](#8-runtime-vs-compile-time-dispatch) | 4 | `Send(object)` matches typed `Send<T>`, 500 parallel |
| [Background service patterns](#9-background-service-patterns) | 3 | Scoped mediator in hosted service loops, cancellation |
| [Stress testing](#10-stress-testing) | 3 | 5000 sequential, 100 parallel streams, mixed workload |
| [Failure injection](#11-failure-injection) | 4 | Cancellation mid-pipeline, retry recovery, intermittent failures |
| [Allocation regression](#12-allocation-regression) | 3 | Per-request allocation budget, disposal verification, memory stability |
| [Timeout and deadlock detection](#13-timeout-and-deadlock-detection) | 3 | Slow behavior chains, nested Send, thread pool starvation |
| [Chaos scenarios](#14-chaos-scenarios) | 3 | Random delays + random exceptions + high concurrency |

---

## Why this matters

| Production risk | Test category that covers it |
|---|---|
| Race conditions under load | Concurrency, Stress, Chaos |
| Memory leaks or GC pressure | Allocation regression |
| Flaky handler failures | Failure injection |
| Deadlocks from nested dispatch | Timeout and deadlock detection |
| DI lifetime violations | Dependency injection lifetimes |
| AOT deployment failures | Native AOT safety |
| Test double incompatibility | Expression tree safety |
| Pipeline behavior ordering bugs | Deep pipeline |
| Lost messages under concurrency | Concurrency, Background service patterns |

---

## Comparison with other libraries

Test suite analysis based on public GitHub repositories (verified June 2025):

| Library | Repository | Unit tests | Pipeline tests | Concurrency tests | Failure injection | Chaos tests |
|---|---|:---:|:---:|:---:|:---:|:---:|
| **DSoftStudio.Mediator** | [DSoftStudio/Mediator](https://github.com/DSoftStudio/Mediator) | ✅ | ✅ | ✅ (2000+) | ✅ | ✅ |
| Mediator (SG) 3.0 | [martinothamar/Mediator](https://github.com/martinothamar/Mediator) | ✅ | ✅ | ❌ | ❌ | ❌ |
| DispatchR 2.1 | [hasanxdev/DispatchR](https://github.com/hasanxdev/DispatchR) | ✅ | Basic | ❌ | ❌ | ❌ |
| MediatR 14.1 | [LuckyPennySoftware/MediatR](https://github.com/LuckyPennySoftware/MediatR) | ✅ | ✅ | ❌ | ❌ | ❌ |

---

## Detailed test catalog

### 1. Multi-project discovery

Validates that source-generated handler discovery works across assembly boundaries.

| Test | What it verifies |
|---|---|
| `AllHandlers_DiscoveredAndResolvable_AcrossAssembly` | `ValidateMediatorHandlers()` resolves every handler from DI, including cross-assembly |
| `Send_HandlerFromDifferentNamespace_ReturnsCorrectResult` | Handler in `Infrastructure/` namespace returns correct result |
| `Publish_NotificationHandlerFromDifferentNamespace_DoesNotThrow` | Notification handler in different namespace is discovered |
| `CreateStream_HandlerFromDifferentNamespace_YieldsAllItems` | Stream handler in different namespace yields all items |

**Production impact:** Multi-project solutions where domain handlers and API handlers live in separate assemblies.

---

### 2. Dependency injection lifetimes

Validates Scoped, Transient, and Singleton lifetimes work correctly through the mediator pipeline.

| Test | What it verifies |
|---|---|
| `SameScope_ScopedDependency_SameInstance` | Same scope → same `ScopedCorrelation.Id` across multiple Send calls |
| `DifferentScopes_ScopedDependency_DifferentInstances` | Different scopes → different scoped instances, same singleton |
| `TransientDependency_DifferentAcrossScopes` | Different scopes → different `TransientStamp.Id` |
| `SingletonCounter_SharedAcrossParallelScopes` | 50 parallel scopes share the same `SingletonCounter`, no lost increments |

**Production impact:** ASP.NET Core request scopes, EF Core `DbContext` lifetime, per-request correlation IDs.

---

### 3. Deep pipeline

Validates 6 behaviors in correct order with exception handling.

| Test | What it verifies |
|---|---|
| `SixBehaviors_ExecuteInCorrectNestingOrder` | Logging → Validation → Auth → Metrics → Retry → Caching — all fire with correct enter/exit pairing |
| `DeepPipeline_HandlerThrows_ExceptionPropagates` | Exception propagates through all 6 behaviors; retry behavior retries once |
| `DeepPipeline_WithExceptionHandler_FallbackResultReturned` | `IRequestExceptionHandler` intercepts and provides fallback result |
| `DeepPipeline_MixedSyncAsync_CompletesCorrectly` | 10 consecutive sends through 6-behavior pipeline — stability |

**Production impact:** Enterprise pipelines with logging, validation, authorization, metrics, retry, and caching behaviors stacked together.

---

### 4. Concurrency

Validates correctness under high parallelism with no race conditions.

| Test | What it verifies |
|---|---|
| `Send_2000Parallel_AllResultsCorrect` | 2000 parallel Send calls — every result matches `seed × 2` |
| `Publish_1000Parallel_AllNotificationsReceived` | 1000 parallel Publish calls — all notifications received, no duplicates |
| `MixedSendAndPublish_1000Parallel_NoInterference` | 500 parallel Send + 500 parallel Publish — no cross-contamination |

**Production impact:** High-throughput APIs handling thousands of concurrent requests.

---

### 5. Native AOT safety

Validates that `PrecompilePipelines()` produces AOT-compatible service registrations.

| Test | What it verifies |
|---|---|
| `PrecompilePipelines_RemovesAllOpenGenericBehaviors` | Open-generic `IPipelineBehavior<,>` replaced with closed-generic — no `MakeGenericType` at runtime |
| `PrecompiledBehaviors_FireCorrectly_ForValueAndReferenceTypes` | Behaviors fire for both `int` and `Unit` response types after precompilation |
| `PrecompilePipelines_CalledTwice_StillWorksCorrectly` | Idempotent — calling twice doesn't corrupt registrations |

**Production impact:** Native AOT deployments, trimmed applications, serverless cold start optimization.

---

### 6. Expression tree safety

Validates that Moq/NSubstitute expression trees don't trigger interceptor rewriting.

| Test | What it verifies |
|---|---|
| `ExpressionTree_WithSendCall_DoesNotCrash` | `Expression<Func<ISender, ValueTask<int>>>` compiles without CS8652 |
| `ExpressionTree_WithPublishCall_DoesNotCrash` | `Expression<Func<IPublisher, Task>>` compiles without CS8652 |

**Production impact:** Test projects using Moq `Setup(() => sender.Send(...))` pattern.

---

### 7. Complex generics

Validates nullable types and complex generic constraints through the full pipeline.

| Test | What it verifies |
|---|---|
| `NullableReferenceType_NonNullResult` | `IRequest<string?>` returns non-null correctly |
| `NullableReferenceType_NullResult` | `IRequest<string?>` returns `null` correctly |
| `NullableValueType_NonNullResult` | `IRequest<int?>` returns `42` correctly |
| `NullableValueType_NullResult` | `IRequest<int?>` returns `null` correctly |
| `Stream_WithValueTypeResponse_YieldsCorrectItems` | `IStreamRequest<int>` yields correct sequence |

**Production impact:** Domain models with nullable responses, optional query results.

---

### 8. Runtime vs compile-time dispatch

Validates `Send(object)` runtime dispatch matches typed `Send<TRequest, TResponse>`.

| Test | What it verifies |
|---|---|
| `TypedSend_vs_ObjectSend_SameResult` | Both dispatch paths return identical results |
| `ObjectSend_UnitResponse_ReturnsUnit` | `Send(object)` with void request returns `Unit.Value` |
| `ObjectSend_UnregisteredType_ThrowsInvalidOperationException` | Unregistered type throws — no silent default |
| `ObjectSend_500Parallel_AllCorrect` | 500 parallel `Send(object)` calls — no race conditions in `FrozenDictionary` |

**Production impact:** Generic middleware, message routing, dynamic dispatch scenarios.

---

### 9. Background service patterns

Validates mediator usage from `IHostedService` / background worker patterns.

| Test | What it verifies |
|---|---|
| `BackgroundWorker_ProcessesJobsInScopes` | 20 sequential jobs in individual scopes — correct DI pattern |
| `BackgroundWorker_CancellationToken_Respected` | Cancellation token propagates through the pipeline |
| `MultipleWorkers_ConcurrentScopes_NoLostIncrements` | 10 workers × 50 jobs — 500 total, singleton counter matches |

**Production impact:** Background job processors, queue consumers, scheduled tasks.

---

### 10. Stress testing

Sustained load testing with consistent result verification.

| Test | What it verifies |
|---|---|
| `SequentialStress_5000Requests_AllCorrect` | 5000 sequential requests — no state corruption, consistent results |
| `ParallelStreams_100Consumers_AllItemsReceived` | 100 parallel stream consumers × 100 items — 10,000 total, no lost items |
| `MixedWorkload_SendPublishStream_NoInterference` | 200 parallel iterations of Send + Publish + Stream — no cross-contamination |

**Production impact:** Sustained API load, batch processing, event-driven architectures.

---

### 11. Failure injection

Validates behavior under adverse conditions.

| Test | What it verifies |
|---|---|
| `CancellationDuringHandler_ThrowsOperationCanceledException` | 5s handler cancelled after 50ms — `OperationCanceledException` propagates |
| `RetryBehavior_RecoversFlakyHandler` | Handler fails once, retry behavior recovers on second attempt |
| `RetryExhausted_ExceptionPropagates` | Handler always fails — retry exhausted, exception reaches caller |
| `IntermittentFailures_ParallelSend_NoCrossContamination` | 500 parallel sends, ~50% fail — each result is success or exception, never wrong value |

**Production impact:** Transient database failures, network timeouts, circuit breaker patterns.

---

### 12. Allocation regression

Validates memory behavior under sustained load.

| Test | What it verifies |
|---|---|
| `SequentialRequests_AllocationPerRequestBounded` | 10,000 requests — per-request allocation < 10 KB (validates no cumulative leak) |
| `ScopedDisposal_DisposableResourcesReleased` | `ScopedCorrelation.Disposed == true` after scope ends |
| `ParallelScopes_StableMemory` | 5 waves × 100 parallel scopes — memory growth < 10 MB after GC |

**Production impact:** Long-running services, memory-constrained environments, GC pressure monitoring.

---

### 13. Timeout and deadlock detection

Validates async pipeline doesn't deadlock under contention.

| Test | What it verifies |
|---|---|
| `SlowBehaviorChain_CompletesWithinTimeout` | 3 slow behaviors (10ms each) + handler — completes within 5s timeout |
| `NestedSendInsideBehavior_NoDeadlock` | Behavior sends `Ping` inside `NestedOuterPing` pipeline — no deadlock |
| `ParallelSend_WithSlowBehaviors_CompletesWithinTimeout` | 200 parallel sends with slow behaviors — all complete within 30s |

**Production impact:** Complex pipelines where behaviors invoke the mediator, thread pool exhaustion under load.

---

### 14. Chaos scenarios

Random delays + random exceptions + high concurrency.

| Test | What it verifies |
|---|---|
| `ChaosMode_1000Parallel_MostSucceed` | 1000 parallel sends, 10% failure rate — ≥70% succeed, total = 1000 |
| `MixedWorkloadWithChaos_NoCrossContamination` | Send (with chaos) + Publish (no chaos) — notifications always arrive |
| `HighFailureRate_WithExceptionHandler_GracefulDegradation` | 50% failure rate + exception handler — all 500 requests return (success or fallback), zero exceptions to caller |

**Production impact:** Unreliable downstream services, partial outages, graceful degradation patterns.

---

## Running the tests

```shell
dotnet test tests/DSoftStudio.Mediator.Tests --filter "FullyQualifiedName~Integration.Enterprise"
```

Or run the full suite:

```shell
dotnet test tests/DSoftStudio.Mediator.Tests
```

All 48 enterprise integration tests run as part of the CI pipeline on every push.

---

Run the tests. Read the results. Decide.

---

[&larr; Back to Documentation](../index.md) · [Performance Design](performance.md) · [Benchmarks](../benchmarks.md)
