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
- **Constant allocations** — zero per Send on .NET 11, 72 B on .NET 10, whatever the pipeline depth
- **Native AOT safe** — no reflection or runtime codegen on any dispatch path; publishes and runs as a native binary
- **Deterministic dispatch** — no inheritance surprises, no duplicate handlers
- **MediatR-compatible API** — drop-in migration

> **Near-zero overhead in real pipelines.** A validation → logging → metrics → async-write pipeline
> costs 112 ns against 100 ns for calling the handler directly (.NET 11). The next closest mediator
> costs 238 ns.

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
- Native AOT: publishes and runs as a native binary, zero trim/AOT warnings (verified on win-x64)
- Multi-project solutions with source generators

> [Full test catalog →](https://docs.dsoftstudio.com/mediator/architecture/production-validation)

---

## Why this matters in production

### The real question

Does your mediator add cost to your pipeline?

Microbenchmark tables show framework overhead in isolation — nanoseconds that never exist alone in a real system. The meaningful question is: **does the mediator add cost to your actual pipeline?**

### What we measured

A realistic enterprise pipeline — **Validation → Logging → Metrics → async database write** — with 3 pipeline behaviors and dependency injection. The kind of pipeline you ship to production.

Each library is measured against **its own** direct call, so the comparison is "what did the mediator
add", not "whose machine was faster".

**.NET 10**

| Library | Direct call | Mediator pipeline | vs direct | Memory |
|---|---:|---:|---:|---:|
| **DSoftStudio.Mediator** | 669 ns | **682 ns** | **1.02×** | **254 B** |
| DispatchR 2.3 | 672 ns | 694 ns | 1.03× | 255 B |
| Mediator (Source Gen) 3.0 | 689 ns | 732 ns | 1.06× | 398 B |
| MediatR 14.2 | 668 ns | 848 ns | 1.27× | 1,032 B |

**.NET 11** — runtime async removes most of the async overhead, so the handler gets ~7× cheaper and
what the mediator adds stops hiding inside it:

| Library | Direct call | Mediator pipeline | vs direct | Memory |
|---|---:|---:|---:|---:|
| **DSoftStudio.Mediator** | 100 ns | **112 ns** | **1.12×** | **144 B** |
| Mediator (Source Gen) 3.0 | 97 ns | 238 ns | 2.46× | 335 B |
| DispatchR 2.3 | 100 ns | 247 ns | 2.47× | 263 B |
| MediatR 14.2 | 99 ns | 351 ns | 3.56× | 1,016 B |

> On .NET 10 every source-generated mediator looks free, because a 669 ns handler hides 13 ns of
> dispatch. On .NET 11 the handler is 100 ns and the difference is plain: this library adds 12 ns,
> the others add 140 to 250.

### What this reveals

Four things that isolated microbenchmarks hide:

**GC pressure compounds at scale.**
MediatR allocates 1,016 B per request in this pipeline. At 10k req/s that is ~10 MB/s of short-lived Gen0 objects. This library allocates 144 B — less than calling the handler directly, which allocates 168 B. Under sustained load the difference shows up as GC pause frequency, not as nanoseconds in a benchmark table.

**Allocation profile determines tail latency.**
More GC collections = more variance in p99/p999 response times. Constant-allocation pipelines produce tighter latency distributions. This matters more than mean latency in any SLA-bound system.

**Pipeline depth shouldn't change your cost.**
This library allocates nothing per `Send` on .NET 11 whether you have 0, 3 or 5 behaviors — behaviors chain through interface dispatch rather than delegate wrapping, so there is nothing per link to allocate. MediatR goes 248 B → 1,024 B as you add behaviors, because each one wraps a new delegate and closure. Latency scales the same way: 2.7 ns at zero behaviors to 6.6 ns at five, against 40.9 → 143.5 ns.

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
| **No reflection at runtime** | No `MakeGenericType`, `Expression.Compile`, `Reflection.Emit` or assembly scanning on any dispatch path. Enforced by a test that scans the shipped assemblies for those APIs, not by convention. |
| **Compile-time pipeline transparency** | The full behavior chain is visible in generated code — inspectable, debuggable, and deterministic. No runtime assembly of middleware. |
| **Deterministic notification dispatch** | Compile-time exact-type routing. Publishing `DerivedEvent` never invokes `INotificationHandler<BaseEvent>`. |
| **AOT-safe by construction** | Open-generic registrations are rewritten to closed ones at startup, so the container never constructs a type at runtime. Where a behavior cannot be named from generated code the rewrite is impossible, and **DSOFT011** says so at build time rather than letting it fail in a published binary. |
| **Constant-allocation pipeline** | Zero per Send on .NET 11 and 72 B on .NET 10, regardless of behavior count. Zero-alloc Publish on both. |

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

### Register at startup

**Recommended — single-call registration (v1.2.0+)**

```csharp
services.AddMediator(builder =>
{
    // Register pipeline behaviors, processors, etc.
    builder.AddOpenBehavior(typeof(LoggingBehavior<,>));
    builder.AddRequestPreProcessor<ValidationPreProcessor>();
    builder.AddParallelNotificationPublisher();
});
```

> **No behaviors to register?** You still need the builder callback — pass an empty lambda:
>
> ```csharp
> services.AddMediator(_ => { });
> ```
>
> This registers handlers, precompiles pipelines, and freezes dispatch — all in one call.

**Manual registration (v1.1.x style)**

If you omit the builder callback, `AddMediator()` only registers core services (`IMediator`, `ISender`, `IPublisher`). You must chain the remaining steps yourself:

```csharp
services.AddMediator()               // Core services only
    .RegisterMediatorHandlers()       // Discover and register all handlers
    .PrecompilePipelines()            // Request dispatch table
    .PrecompileNotifications()        // Notification dispatch table
    .PrecompileStreams();             // Stream dispatch table
```

> This is the v1.1.x pattern and remains fully supported for advanced scenarios where you need to insert registrations between steps.

> **All three, not just `PrecompilePipelines()`.** They arm three separate dispatch tables. Stopping
> after the first leaves `Publish` reaching no handlers and `CreateStream` producing nothing — with no
> build error and no exception, because an empty table is indistinguishable from an application that
> has no notifications. `AddMediator(builder => { })` calls all three for you, which is the main
> reason to prefer it.

### Multi-project setup (hexagonal / clean architecture)

Install the full package **only** in the composition root (API / host). Application and domain layers reference only the abstractions:

```shell
# API / Host project (composition root) — source generator + DI registration
dotnet add package DSoftStudio.Mediator

# Application layer — contracts only (IRequest, IRequestHandler, IPipelineBehavior, etc.)
dotnet add package DSoftStudio.Mediator.Abstractions
```

The source generator runs in the API project and automatically discovers handlers from referenced assemblies. Handlers must be `public` — internal handlers require `[InternalsVisibleTo]` (see [DSOFT005](https://docs.dsoftstudio.com/mediator/architecture/design-notes)).

```
Host / API           → DSoftStudio.Mediator              (AddMediator + source generator)
Application          → DSoftStudio.Mediator.Abstractions  (handlers, requests, behaviors)
Domain               → (no mediator dependency)
Infrastructure       → (no mediator dependency)
```

### Send a request

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

Measured on .NET 11, each library against its own direct call. Allocation is the pipeline's bytes
over the same handler called directly.

| | DSoft | Mediator (SG) | DispatchR | MediatR |
|---|:---:|:---:|:---:|:---:|
| Pipeline latency vs direct call | **1.12×** | 2.46× | 2.47× | 3.56× |
| Pipeline allocation vs direct call | **0.86×** | 1.99× | 1.57× | 6.05× |
| Publishes under Native AOT | ✅ | ✅ | ❌ | ❌ |

This library is additionally validated under failure injection, chaos scenarios and 2000+ concurrent
dispatches — see [Production validation](#production-validation).

> Reviewed 16 September 2026 against MediatR [`916ef1b`](https://github.com/jbogard/MediatR),
> Mediator [`8b87a0e`](https://github.com/martinothamar/Mediator) and DispatchR
> [`69be512`](https://github.com/hasanxdev/DispatchR): none of the three public repositories
> contains failure-injection, chaos, or sustained-concurrency test suites. Their suites are
> thorough elsewhere — MediatR covers six DI containers, Mediator has extensive source-generator
> snapshot coverage — so this is a difference in emphasis, not in rigour. Dated and pinned because
> it describes three moving projects: re-check it before relying on it.

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
| Runtime-typed `Send(object)` | ✅ | ✅ | ? | ✅ |
| Compile-time pipeline | ✅ | ✅ | ❌ | ❌ |
| MediatR-style API | ✅ | ✅ | ❌ | ✅ |

`?` means not established. DispatchR may well have a runtime-typed send; no benchmark suite was
written for it, so the honest answer is that nobody here checked.

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
| Runtime Dispatch | `Send(object)` compiles to a generated type switch — no reflection | [Docs](https://docs.dsoftstudio.com/mediator/features/runtime-dispatch) |
| Streams | `IAsyncEnumerable<T>` with pipeline support | [Docs](https://docs.dsoftstudio.com/mediator/concepts/streams) |
| Handler Validation | `ValidateMediatorHandlers()` — fail fast at startup | [Docs](https://docs.dsoftstudio.com/mediator/features/handler-validation) |
| Native AOT | Publishes and runs native, zero IL warnings (win-x64 verified) | [Docs](https://docs.dsoftstudio.com/mediator/architecture/native-aot) |

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

## Benchmarks

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/), each library in an **isolated process**
so no library warms the runtime for the next. Compared against
[Mediator](https://github.com/martinothamar/Mediator) 3.0.2,
[DispatchR](https://github.com/hasanxdev/DispatchR) 2.3.1, and
[MediatR](https://github.com/jbogard/MediatR) 14.2.0.

### Latency per operation — .NET 11

| Operation | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| `Send()` | **2.7 ns** | 9.8 ns | 27.1 ns | 40.9 ns |
| `Send()` 5 behaviors | **6.6 ns** | 27.2 ns | 31.2 ns | 143.5 ns |
| `Publish()` | **2.4 ns** | 6.3 ns | 32.3 ns | 112.9 ns |
| `CreateStream()` | **30.7 ns** | 31.8 ns | 54.0 ns | 112.8 ns |

On .NET 10 the same order holds with everything roughly twice as slow: `Send()` 5.7 / 15.0 / 33.5 /
41.7 ns, and `Send()` with 5 behaviors 11.4 / 28.9 / 34.6 / 141.6 ns.

### Allocations per operation

| Operation | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| `Send()` | **0 B** | **0 B** | **0 B** | 248 B |
| `Send()` 5 behaviors | **0 B** | **0 B** | **0 B** | 1,024 B |
| `Publish()` | **0 B** | **0 B** | **0 B** | 752 B |
| `CreateStream()` | 88 B | 88 B | 88 B | 464 B |

Three of the four allocate nothing per dispatch on .NET 11 — runtime async removes the state machine
that cost 72 B on .NET 10. MediatR allocates because its pipeline wraps a delegate per behavior, which
is a design choice rather than a runtime one: its figure barely moved between the two platforms.

### Startup

Startup is measured one process per sample, because it is a property of a process and cannot be seen
from inside a warm one. **Added** is time-to-first-request minus the same measurement on a container
holding one trivial service — the .NET and DI floor every library pays and none of them causes.

| Library | Added (.NET 11) | Memory | Added (.NET 10) | Memory |
|---|---:|---:|---:|---:|
| Mediator (Source Gen) | **10.2 ms** | 154 KB | **11.8 ms** | 157 KB |
| DispatchR | 12.1 ms | 651 KB | 13.8 ms | 749 KB |
| **DSoftStudio.Mediator** | 22.7 ms | **32 KB** | 26.1 ms | **34 KB** |
| MediatR | 31.5 ms | 1,680 KB | 38.7 ms | 2,204 KB |

**This library is third of four on startup time, and that is the trade.** Most of it is
`PrecompilePipelines()` building the dispatch chains up front — the work that buys the 2.7 ns dispatch
and the 32 KB, which is 50× less startup garbage than MediatR. On a service the up-front cost is paid
once and amortises within seconds; on a short-lived process that never gets there, it does not.

### Native AOT

| | Publishes under NativeAOT | Why |
|---|---|---|
| **DSoftStudio.Mediator** | ✅ verified, zero IL warnings | dispatch is generated, no reflection |
| Mediator (Source Gen) | ✅ | same approach |
| DispatchR | ❌ | scans assemblies at runtime |
| MediatR | ❌ | `MakeGenericType` on the dispatch path |

Verified by publishing a native binary and running it: typed `Send`, `Send(object)`, `Publish`,
streams and pipeline behaviors all dispatch correctly, including value-type responses, which is the
case that breaks a reflective mediator outright. Startup for that binary measured 8.3 ms against
78.9 ms for the same source on the JIT. Verified on win-x64.

The caching companion needs one extra step under AOT — `HybridCache` serializes what it caches and
only ships serializers for `string` and `byte[]`. The generator raises **DSOFT012** at build time
naming the type you need to register, rather than letting it fail on the first cached request.

> Full results: [`benchmarks/BENCHMARKS.md`](benchmarks/BENCHMARKS.md) (.NET 10) and
> [`benchmarks/BENCHMARKS-net11.0.md`](benchmarks/BENCHMARKS-net11.0.md) (.NET 11).
> The realistic-pipeline comparison is [above](#what-we-measured) — it is the number that matters
> most, and it lives in one place so the two cannot drift apart.

---

## Migrating from MediatR

Mechanical code changes. No architectural rewrite.

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Handler return | `Task<TResponse>` | `ValueTask<TResponse>` |
| Behavior `next` | `RequestHandlerDelegate<T>` | `IRequestHandler<TReq, TRes>` |
| Calling next | `await next()` | `await next.Handle(req, ct)` |
| Pre/Post return | `Task` | `ValueTask` |
| Handler lifetime | All Transient | Derived from the constructor: stateless or all-Singleton deps → Singleton, any Scoped → Scoped, any Transient → Transient |
| Component lifetime | `AddOpenBehavior` → Transient | `AddOpenBehavior` → **Scoped** (one Transient component makes the whole chain Transient) |
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
| [`minimal-api`](samples/minimal-api) | Minimal API recipes (CRUD, pagination, auth) | 6100 |

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
- **Advanced** — [Caching Patterns](https://docs.dsoftstudio.com/mediator/advanced/caching-patterns) · [Pipeline Patterns](https://docs.dsoftstudio.com/mediator/advanced/pipeline-patterns) · [Minimal API Integration](https://docs.dsoftstudio.com/mediator/advanced/minimal-api-integration)

---

A mediator should not be part of your performance budget.

---

## Support

❤️ [Sponsor on GitHub](https://github.com/sponsors/yandersr)

---

## License

[MIT](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)