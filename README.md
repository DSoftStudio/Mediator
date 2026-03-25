![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![CI](https://github.com/DSoftStudio/Mediator/actions/workflows/ci.yml/badge.svg)](https://github.com/DSoftStudio/Mediator/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=DSoftStudio_Mediator&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=DSoftStudio_Mediator)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
![NativeAOT](https://img.shields.io/badge/NativeAOT-compatible-success)

# A mediator that disappears in your pipeline.

A mediator with zero structural cost.

*The cost of your pipeline should be your code — not your mediator.*

Designed for high-throughput, latency-sensitive systems where predictability matters.

Source-generated mediator for .NET.

- **Zero structural overhead** — direct-call equivalent
- **Constant allocations** — 72 B per Send (independent of pipeline depth)
- **Native AOT safe** — no reflection or runtime codegen in any code path
- **Deterministic dispatch** — no inheritance surprises, no duplicate handlers
- **MediatR-compatible API** — drop-in migration

> **Zero overhead in real pipelines.** Performance parity with direct calls — 667 ns vs 674 ns.

**No surprises. No hidden cost. No runtime magic.**

---

## Why this exists

Most mediator implementations introduce hidden cost as systems grow — allocations scale with pipeline depth, execution flow becomes harder to trace, and tail latency becomes unpredictable.

This library removes those costs — by design.

### What's different

- Compile-time pipeline — no runtime composition
- Fully inspectable execution — no hidden middleware chains
- Constant allocation — independent of pipeline depth

### Why this feels different

With traditional mediators:
- you call `Send()`
- the pipeline is assembled at runtime
- the execution path is implicit

With DSoftStudio.Mediator:
- the pipeline is compiled ahead of time
- the execution is visible in generated code
- the call is fully predictable

No hidden composition — everything is generated and visible. No runtime surprises.

**For teams that care about:**
- Predictable latency (p99)
- Minimal GC pressure
- Debuggable pipelines

```csharp
var result = await mediator.Send(new Ping()); // that's it
```

> [Quick Start →](#quick-start)

---

## Production validation

This library is not only benchmarked — it's validated under real-world conditions.

- 2000+ parallel requests (Send / Publish)
- Deep pipelines (6+ behaviors with retry, exceptions, async flows)
- Failure injection (flaky handlers, retries, partial failures)
- Chaos scenarios (random delays, intermittent faults)
- Native AOT & trimming compatibility
- Multi-project solutions with source generators

> [Full test catalog →](https://docs.dsoftstudio.com/mediator/architecture/production-validation)

---

## Why this matters in production

### The real question

Does your mediator add cost to your pipeline?

Microbenchmark tables show framework overhead in isolation — nanoseconds that never exist alone in a real system. The meaningful question is: **does the mediator add cost to your actual pipeline?**

### What we measured

A realistic enterprise pipeline — **Validation → Logging → Metrics → async database write** — with 3 pipeline behaviors and dependency injection. The kind of pipeline you ship to production.

| Library | Pipeline | Latency | Memory | vs Direct Call |
|---|---|---:|---:|---|
| **DSoftStudio.Mediator** | Direct call | 674 ns | 271 B | — |
| | **Mediator pipeline** | **667 ns** | **255 B** | **0.99×** |
| | | | | |
| DispatchR 2.1 | Direct call | 661 ns | 271 B | — |
| | Mediator pipeline | 667 ns | 255 B | 1.01× |
| | | | | |
| Mediator (Source Gen) 3.0 | Direct call | 679 ns | 270 B | — |
| | Mediator pipeline | 718 ns | 397 B | 1.06×, 1.5× alloc |
| | | | | |
| MediatR 14.1 | Direct call | 714 ns | 270 B | — |
| | Mediator pipeline | 857 ns | 1,032 B | 1.20×, **3.8× alloc** |

> **The mediator layer adds zero measurable overhead.** The cost is your handler — not the framework.

### What this reveals

Four things that isolated microbenchmarks hide:

**GC pressure compounds at scale.**
MediatR allocates 1,032 B per request in this pipeline. At 10k req/s, that's ~10 MB/s of short-lived Gen0 objects. DSoft allocates 255 B — the same as calling the method directly. Under sustained load, the difference shows up as GC pause frequency, not as nanoseconds in a benchmark table.

**Allocation profile determines tail latency.**
More GC collections = more variance in p99/p999 response times. Constant-allocation pipelines produce tighter latency distributions. This matters more than mean latency in any SLA-bound system.

**Pipeline depth shouldn't change your cost.**
DSoft allocates 72 B per Send whether you have 0, 3, or 5 behaviors — the allocation is constant because behaviors chain through interface dispatch, not delegate wrapping. MediatR allocates 272 B → 800 B → 1,088 B as you add behaviors, because each behavior wraps a new delegate and closure.

**Implicit pipelines can become opaque as they grow.**
In MediatR, the behavior chain is assembled at runtime through service resolution. As systems grow, understanding the exact execution flow often requires tracing through middleware layers and the DI container. DSoftStudio.Mediator takes a different approach: the full pipeline is generated at compile time. The behavior chain is visible in source-generated code, inspectable in your IDE, and fully deterministic. What you register is what runs, in the order you registered it.

This is not about being faster in microbenchmarks — it's about keeping your system predictable and understandable as it scales.

---

## What this means

In real systems, performance issues don't come from averages.

They come from:
- GC pressure
- tail latency (p99, p999)
- unexpected allocations
- failure scenarios

DSoftStudio.Mediator is designed to:
- behave like a direct call
- remain stable under load
- avoid hidden runtime costs

---

## Key guarantees

| | |
|---|---|
| **No runtime resolution** | All dispatch paths are source-generated. No `IServiceProvider.GetService()` on the hot path. |
| **No hidden allocations** | Behavior chains use interface dispatch (`IRequestHandler<,>`), not `Func<>` delegates. No closures. |
| **No reflection** | No `MakeGenericType`, `Expression.Compile`, or assembly scanning in any code path. |
| **Compile-time pipeline transparency** | The full behavior chain is visible in generated code — inspectable, debuggable, and deterministic. No runtime assembly of middleware. |
| **Deterministic notification dispatch** | Compile-time exact-type routing. Publishing `DerivedEvent` never invokes `INotificationHandler<BaseEvent>`. |
| **AOT-safe by construction** | Structural property of the architecture, not a runtime guard or opt-in flag. |
| **Constant-allocation pipeline** | 72 B per Send regardless of behavior count. Zero-alloc Publish. |

---

## Quick Start

```shell
dotnet add package DSoftStudio.Mediator
```

```csharp
public record Ping() : IRequest<int>;

public class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken ct)
        => new ValueTask<int>(42);
}
```

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines();
```

```csharp
var result = await mediator.Send(new Ping());
```

> [Quick Start Guide](https://docs.dsoftstudio.com/mediator/getting-started/quick-start) · [Installation](https://docs.dsoftstudio.com/mediator/getting-started/installation)

---

## When to use this

**Use DSoftStudio.Mediator when:**
- You need a mediator that adds zero overhead to your pipeline
- Native AOT or trimming is required
- Predictable p99 latency matters — high-throughput APIs, real-time systems
- You want MediatR's API without MediatR's allocation profile
- GC pressure is a concern at scale

**Use MediatR when:**
- Performance is not a primary concern
- You need runtime flexibility (dynamic handler discovery, inheritance-based notification dispatch)
- Your team depends on MediatR's established ecosystem and community

**This library is not:**
- A message bus — use MassTransit, NServiceBus, or Azure Service Bus
- An event sourcing framework
- A replacement for direct method calls when you don't need the mediator pattern

---

## Comparison

| Feature | DSoft | Mediator (SG) | DispatchR | MediatR |
|---|:---:|:---:|:---:|:---:|
| Structural overhead | None | Low | Reduced | High |
| Pipeline alloc overhead | None | +1.5× | None | +3.8× |
| Failure-tested | ✅ | ❌ | ❌ | ❌ |
| Chaos-tested | ✅ | ❌ | ❌ | ❌ |
| Concurrency-tested (2000+) | ✅ | ❌ | ❌ | ❌ |
| AOT-safe | ✅ | ✅ | ❌ | ❌ |

Among the libraries benchmarked — Mediator (SG), DispatchR, and MediatR — DSoftStudio.Mediator is the only one validated for failure, chaos, and concurrency, with zero overhead in production pipelines.

---

## Feature Comparison

| Feature | DSoft | Mediator (SG) | DispatchR | MediatR |
|---|:---:|:---:|:---:|:---:|
| Source generators | ✅ | ✅ | ❌ | ❌ |
| Native AOT compatible | ✅ | ✅ | ❌ | ❌ |
| Reflection-free hot path | ✅ | ✅ | ❌ | ❌ |
| Zero-alloc pipeline | ✅ | ✅ | ✅ | ❌ |
| Auto-Singleton handlers | ✅ | ❌ | ❌ | ❌ |
| Self-handling requests | ✅ | ❌ | ❌ | ❌ |
| Exact-type notification dispatch | ✅ | ❌ | ✅ | ❌ |
| Runtime-typed `Send(object)` | ✅ | ❌ | ❌ | ✅ |
| Compile-time pipeline | ✅ | ✅ | ❌ | ❌ |
| MediatR-style API | ✅ | ✅ | ❌ | ✅ |

---

## Mental model

This mediator does not *execute* your pipeline.

It **becomes** your pipeline at compile time.

---

## Execution Model

```text
Send(request)
  → Precompiled pipeline (compile-time chain)
    → Behavior1 → Behavior2 → ... → BehaviorN
      → Handler (direct call, no GetService)
        → ValueTask<TResponse>

Publish(notification)
  → Closed dispatch table (compile-time, exact type)
    → Handler1, Handler2, ... → ValueTask (zero alloc)
```

No delegates. No closures. No `IServiceProvider` on the hot path. Every call is a direct typed invocation through a precompiled chain.

---

## Features

| Feature | Description | Docs |
|---|---|---|
| Pipeline Behaviors | Zero-allocation chains via interface dispatch | [Docs](https://docs.dsoftstudio.com/mediator/features/pipeline-behaviors) |
| Pre/Post Processors | Before/after hooks without chain responsibility | [Docs](https://docs.dsoftstudio.com/mediator/features/pre-post-processors) |
| CQRS | `ICommand<T>` / `IQuery<T>` with semantic aliases | [Docs](https://docs.dsoftstudio.com/mediator/concepts/cqrs) |
| Self-Handling Requests | `static Execute` in request type — no handler class | [Docs](https://docs.dsoftstudio.com/mediator/features/self-handling-requests) |
| Notifications | Exact-type compile-time dispatch | [Docs](https://docs.dsoftstudio.com/mediator/concepts/notifications) |
| Runtime Dispatch | `Send(object)` via `FrozenDictionary` — AOT-safe | [Docs](https://docs.dsoftstudio.com/mediator/features/runtime-dispatch) |
| Streams | `IAsyncEnumerable<T>` with pipeline support | [Docs](https://docs.dsoftstudio.com/mediator/concepts/streams) |
| Handler Validation | `ValidateMediatorHandlers()` — fail fast at startup | [Docs](https://docs.dsoftstudio.com/mediator/features/handler-validation) |
| Native AOT | Full AOT and trimming compatibility | [Docs](https://docs.dsoftstudio.com/mediator/architecture/native-aot) |

---

## Ecosystem

**Contracts** — [`DSoftStudio.Mediator.Abstractions`](https://www.nuget.org/packages/DSoftStudio.Mediator.Abstractions) · Reference from domain/application layers. No runtime dependency. [Docs](https://docs.dsoftstudio.com/mediator/getting-started/installation)

**Observability** — [`DSoftStudio.Mediator.OpenTelemetry`](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry) · Tracing + metrics for Send, Publish, and Stream dispatch paths. [Docs](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry)

**Validation** — [`DSoftStudio.Mediator.FluentValidation`](https://www.nuget.org/packages/DSoftStudio.Mediator.FluentValidation) · Automatic request validation via pipeline behavior. [Docs](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation)

**Caching** — [`DSoftStudio.Mediator.HybridCache`](https://www.nuget.org/packages/DSoftStudio.Mediator.HybridCache) · L1 + L2 response caching via Microsoft HybridCache. [Docs](https://docs.dsoftstudio.com/mediator/integrations/hybridcache)

---

## Design Notes

### Notification dispatch by exact type

Notifications are dispatched by **exact compile-time type**, not by runtime inheritance hierarchy. Publishing a `DerivedEvent` that extends `BaseEvent` invokes only handlers registered for `DerivedEvent` — `INotificationHandler<BaseEvent>` is **not** invoked.

MediatR dispatches notifications via `GetServicesAssignableTo`, which walks the inheritance chain at runtime through reflection. This causes the well-known duplicate handler problem: a handler registered for a base type fires for every derived type, leading to unintended side effects that are difficult to diagnose.

DSoftStudio.Mediator avoids this entirely. The source generator emits a closed dispatch table at compile time — each notification type maps to exactly its registered handlers with no runtime type inspection. The result is deterministic dispatch with zero reflection overhead.

### Additional design details

Interceptor code generation (Release vs Debug), mock safety, `DSoftMediatorSuppressInterceptors` kill switch, DSOFT004 analyzer, the recommended abstractions-only project pattern, and the `NotificationPublisherFlag` optimization.

→ **[Full Design Notes](https://docs.dsoftstudio.com/mediator/architecture/design-notes)**

---

## Benchmarks (.NET 10)

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 10. Each library runs in an **isolated process** to prevent cross-contamination.
Compared against [Mediator](https://github.com/martinothamar/Mediator) 3.0.1, [DispatchR](https://github.com/hasanxdev/DispatchR) 2.1.1, and [MediatR](https://github.com/jbogard/MediatR) 14.1.

### What matters

- DSoft ≈ direct call in real pipelines
- Constant allocation regardless of behavior count
- No GC amplification under load

### Latency

| Operation | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| `Send()` | **7.2 ns** | 12.2 ns | 33.4 ns | 41.3 ns |
| `Send()` 5 behaviors | **15.6 ns** | 36.8 ns | 54.1 ns | 153.1 ns |
| `Publish()` | **4.5 ns** | 10.6 ns | 35.7 ns | 123.4 ns |
| `CreateStream()` | 45.5 ns | **44.7 ns** | 68.1 ns | 122.9 ns |
| Cold Start | **1.62 µs** | 9.91 µs | 1.88 µs | 3.24 µs |

### Allocations

| Operation | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| `Send()` | 72 B | 72 B | 72 B | 272 B |
| `Send()` 5 behaviors | 72 B | 72 B | 72 B | 1,088 B |
| `Publish()` | **0 B** | **0 B** | **0 B** | 768 B |
| `CreateStream()` | 232 B | 232 B | 232 B | 624 B |

### Realistic Pipeline (Validation → Logging → Metrics → async DB)

| Library | Pipeline | Latency | Memory | Overhead |
|---|---|---:|---:|---:|
| **DSoft** | Direct call | 674 ns | 271 B | — |
| | **Mediator pipeline** | **667 ns** | **255 B** | **0.99×** |
| | | | | |
| DispatchR | Direct call | 661 ns | 271 B | — |
| | Mediator pipeline | 667 ns | 255 B | 1.01× |
| | | | | |
| Mediator (SG) | Direct call | 679 ns | 270 B | — |
| | Mediator pipeline | 718 ns | 397 B | 1.06× |
| | | | | |
| MediatR | Direct call | 714 ns | 270 B | — |
| | Mediator pipeline | 857 ns | 1,032 B | 1.20× |

> Full BenchmarkDotNet results: [`benchmarks/BENCHMARKS.md`](benchmarks/BENCHMARKS.md)

---

## Migrating from MediatR

Mechanical code changes. No architectural rewrite.

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Handler return | `Task<TResponse>` | `ValueTask<TResponse>` |
| Behavior `next` | `RequestHandlerDelegate<T>` | `IRequestHandler<TReq, TRes>` |
| Calling next | `await next()` | `await next.Handle(req, ct)` |
| Pre/Post return | `Task` | `ValueTask` |
| Handler lifetime | All Transient | Stateless → Singleton |
| Namespace | `using MediatR;` | `using DSoftStudio.Mediator.Abstractions;` |

> [Step-by-step Migration Guide](https://docs.dsoftstudio.com/mediator/getting-started/migration-from-mediatr)

---

## Samples

| Sample | Description | Port |
|---|---|---|
| [`basic-api`](samples/basic-api) | Query + Command, Minimal API | 5100 |
| [`pipeline-logging`](samples/pipeline-logging) | LoggingBehavior + ValidationBehavior | 5200 |
| [`domain-events`](samples/domain-events) | INotification, multiple handlers | 5300 |
| [`streaming`](samples/streaming) | IAsyncEnumerable + SSE | 5400 |
| [`di-lifetimes`](samples/di-lifetimes) | Transient / Scoped / Singleton | 5500 |
| [`pre-post-processors`](samples/pre-post-processors) | Pre/Post processor hooks | 5600 |
| [`self-handling`](samples/self-handling) | Self-handling with static Execute | 5700 |
| [`opentelemetry`](samples/opentelemetry) | OTel tracing + metrics | 5800 |
| [`fluent-validation`](samples/fluent-validation) | FluentValidation integration | 5900 |
| [`caching`](samples/caching) | HybridCache integration | 6000 |
| [`mocking`](samples/mocking) | Expression tree detection + mocks | — |
| [`cross-project-mocking`](samples/cross-project-mocking) | 3-project testability architecture | — |

```shell
dotnet run --project samples/basic-api/DSoft.Sample.Api
```

---

## Documentation

[docs.dsoftstudio.com/mediator](https://docs.dsoftstudio.com/mediator)

- **Getting Started** — [Installation](https://docs.dsoftstudio.com/mediator/getting-started/installation) · [Quick Start](https://docs.dsoftstudio.com/mediator/getting-started/quick-start) · [Registration Order](https://docs.dsoftstudio.com/mediator/getting-started/registration-order) · [Migration](https://docs.dsoftstudio.com/mediator/getting-started/migration-from-mediatr)
- **Core Concepts** — [Requests](https://docs.dsoftstudio.com/mediator/concepts/requests-and-handlers) · [Notifications](https://docs.dsoftstudio.com/mediator/concepts/notifications) · [Streams](https://docs.dsoftstudio.com/mediator/concepts/streams) · [CQRS](https://docs.dsoftstudio.com/mediator/concepts/cqrs)
- **Features** — [Pipeline Behaviors](https://docs.dsoftstudio.com/mediator/features/pipeline-behaviors) · [Pre/Post Processors](https://docs.dsoftstudio.com/mediator/features/pre-post-processors) · [Self-Handling](https://docs.dsoftstudio.com/mediator/features/self-handling-requests) · [Runtime Dispatch](https://docs.dsoftstudio.com/mediator/features/runtime-dispatch) · [Validation](https://docs.dsoftstudio.com/mediator/features/handler-validation)
- **Integrations** — [OpenTelemetry](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry) · [FluentValidation](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation) · [HybridCache](https://docs.dsoftstudio.com/mediator/integrations/hybridcache)
- **Architecture** — [Dispatch Pipeline](https://docs.dsoftstudio.com/mediator/architecture/dispatch-pipeline) · [Source Generators](https://docs.dsoftstudio.com/mediator/architecture/source-generators) · [Native AOT](https://docs.dsoftstudio.com/mediator/architecture/native-aot) · [Performance](https://docs.dsoftstudio.com/mediator/architecture/performance) · [Design Notes](https://docs.dsoftstudio.com/mediator/architecture/design-notes) · [Production Validation](https://docs.dsoftstudio.com/mediator/architecture/production-validation)
- **Advanced** — [Caching Patterns](https://docs.dsoftstudio.com/mediator/advanced/caching-patterns) · [Pipeline Patterns](https://docs.dsoftstudio.com/mediator/advanced/pipeline-patterns)

---

A mediator should not be part of your performance budget.

---

## Support

❤️ [Sponsor on GitHub](https://github.com/sponsors/yandersr)

---

## License

[MIT](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)