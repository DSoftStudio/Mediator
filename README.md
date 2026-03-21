![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![CI](https://github.com/DSoftStudio/Mediator/actions/workflows/ci.yml/badge.svg)](https://github.com/DSoftStudio/Mediator/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=DSoftStudio_Mediator&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=DSoftStudio_Mediator)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
![Send](https://img.shields.io/badge/send-7ns-blue)
![Publish](https://img.shields.io/badge/publish-8.5ns-brightgreen)
![Alloc](https://img.shields.io/badge/alloc-72B-orange)
![NativeAOT](https://img.shields.io/badge/NativeAOT-compatible-success)

Source-generated mediator for .NET — compile-time dispatch, zero-allocation pipelines, Native AOT safe, MediatR-compatible API.

Built for hot paths where every nanosecond and every allocation matters.

- **~7 ns Send** (≈0.6 ns over a direct call), **~8.5 ns Publish** — measured with BenchmarkDotNet on .NET 10
- **0 B Publish allocation**
- **72 B per Send** — `ValueTask` boxing shared by all source-generated mediators. 74% less than MediatR (272 B).
- **Native AOT and trimming safe** — no reflection, `MakeGenericType`, or dynamic codegen in any hot path.
- **Auto-Singleton handlers** — stateless handlers detected at compile time, registered as Singleton automatically.
- **MediatR-compatible API** — `IRequest<T>` / `INotification` / `IPipelineBehavior<,>`. Mechanical migration, no rewrite.

Compared against [MediatR](https://github.com/jbogard/MediatR) 14.1. [Full 4-library benchmark results below](#benchmarks-net-10).

| | DSoft | MediatR | Δ |
|---|---:|---:|---|
| `Send()` | 7.1 ns | 42.1 ns | **5.9× faster** |
| `Publish()` | 8.5 ns | 136.1 ns | **16× faster** |
| Send alloc | 72 B | 272 B | **−74%** |
| Publish alloc | 0 B | 768 B | **−100%** |

---

## Quick Start

```shell
dotnet add package DSoftStudio.Mediator
```

Define a request and handler:

```csharp
public record Ping() : IRequest<int>;

public class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken ct)
        => new ValueTask<int>(42);
}
```

Register at startup:

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

Send a request:

```csharp
var result = await mediator.Send(new Ping());
```

> [Quick Start Guide](https://docs.dsoftstudio.com/mediator/getting-started/quick-start) · [Installation](https://docs.dsoftstudio.com/mediator/getting-started/installation)

---

## Why This Library

**Compile-time dispatch**
All dispatch paths are generated at build time. The hot path is a direct method call through a precompiled chain — no `IServiceProvider.GetService()` on every request, no delegate wrapping, no closure allocations.

**Zero-allocation pipeline**
Behavior chains use interface dispatch (`IRequestHandler<TRequest, TResponse>`) instead of `Func<>` delegates. Each behavior calls the next handler directly. No delegate allocation, no closure capture.

**Exact-type notification dispatch**
Notifications dispatch by compile-time type only. Publishing `DerivedEvent` invokes only `INotificationHandler<DerivedEvent>`, never `INotificationHandler<BaseEvent>`. This eliminates the MediatR duplicate handler problem where base-type handlers fire for every derived type. See [Design Notes](#notification-dispatch-by-exact-type).

**Auto-Singleton detection**
Handlers with no constructor dependencies are registered as Singleton automatically. The source generator inspects constructors at compile time — no runtime heuristics. Handlers with DI dependencies remain Transient.

**AOT by construction**
No `MakeGenericType`, no `Expression.Compile`, no assembly scanning. Native AOT and trimming compatibility is a structural property of the architecture, not a runtime guard or opt-in flag.

---

## Execution Model

All dispatch paths are resolved at compile time:

```
Send(request)
  → Precompiled pipeline (compile-time chain)
    → Behavior₁ → Behavior₂ → … → BehaviorN
      → Handler (direct call, no GetService)
        → ValueTask<TResponse>

Publish(notification)
  → Closed dispatch table (compile-time, exact type)
    → Handler₁, Handler₂, … → ValueTask (zero alloc)
```

No delegates. No closures. No `IServiceProvider` on the hot path. Every call is a direct typed invocation through a precompiled chain.

> The mediator becomes a thin, predictable layer — not a runtime system.

---

## When to Use This

**Use DSoftStudio.Mediator when:**
- Latency matters — hot paths, high-throughput APIs, real-time systems
- Native AOT or trimming is required — no reflection, no `MakeGenericType`
- Predictable behavior is non-negotiable — no runtime type walking, no duplicate handler surprises
- You want MediatR's API without MediatR's overhead

**Use MediatR when:**
- Performance is not a primary concern
- You need runtime flexibility (dynamic handler discovery, inheritance-based notification dispatch)
- Your team prefers the established ecosystem and community size

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

## Benchmarks (.NET 10)

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/) against [Mediator](https://github.com/martinothamar/Mediator) 3.0.1, [DispatchR](https://github.com/hasanxdev/DispatchR) 2.1.1, and [MediatR](https://github.com/jbogard/MediatR) 14.1.

### Latency

| Operation | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| `Send()` | **7.1 ns** | 12.5 ns | 33.4 ns | 42.1 ns |
| `Send()` 5 behaviors | **15.5 ns** | 21.2 ns | 53.5 ns | 150.2 ns |
| `Publish()` | **8.5 ns** | 10.2 ns | 35.0 ns | 136.1 ns |
| `CreateStream()` | 45.8 ns | **45.3 ns** | 67.1 ns | 124.2 ns |
| Cold Start | **1.63 µs** | 7.41 µs | 1.91 µs | 3.10 µs |

### Allocations

| Operation | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| `Send()` | 72 B | 72 B | 72 B | 272 B |
| `Send()` 5 behaviors | 72 B | 72 B | 72 B | 1,088 B |
| `Publish()` | 0 B | 0 B | 0 B | 768 B |
| `CreateStream()` | 232 B | 232 B | 232 B | 624 B |

> Full BenchmarkDotNet results in [`/benchmarks`](benchmarks).

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

## Documentation

[docs.dsoftstudio.com/mediator](https://docs.dsoftstudio.com/mediator)

- **Getting Started** — [Installation](https://docs.dsoftstudio.com/mediator/getting-started/installation) · [Quick Start](https://docs.dsoftstudio.com/mediator/getting-started/quick-start) · [Registration Order](https://docs.dsoftstudio.com/mediator/getting-started/registration-order) · [Migration](https://docs.dsoftstudio.com/mediator/getting-started/migration-from-mediatr)
- **Core Concepts** — [Requests](https://docs.dsoftstudio.com/mediator/concepts/requests-and-handlers) · [Notifications](https://docs.dsoftstudio.com/mediator/concepts/notifications) · [Streams](https://docs.dsoftstudio.com/mediator/concepts/streams) · [CQRS](https://docs.dsoftstudio.com/mediator/concepts/cqrs)
- **Features** — [Pipeline Behaviors](https://docs.dsoftstudio.com/mediator/features/pipeline-behaviors) · [Pre/Post Processors](https://docs.dsoftstudio.com/mediator/features/pre-post-processors) · [Self-Handling](https://docs.dsoftstudio.com/mediator/features/self-handling-requests) · [Runtime Dispatch](https://docs.dsoftstudio.com/mediator/features/runtime-dispatch) · [Validation](https://docs.dsoftstudio.com/mediator/features/handler-validation)
- **Integrations** — [OpenTelemetry](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry) · [FluentValidation](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation) · [HybridCache](https://docs.dsoftstudio.com/mediator/integrations/hybridcache)
- **Architecture** — [Dispatch Pipeline](https://docs.dsoftstudio.com/mediator/architecture/dispatch-pipeline) · [Source Generators](https://docs.dsoftstudio.com/mediator/architecture/source-generators) · [Native AOT](https://docs.dsoftstudio.com/mediator/architecture/native-aot) · [Performance](https://docs.dsoftstudio.com/mediator/architecture/performance) · [Design Notes](https://docs.dsoftstudio.com/mediator/architecture/design-notes)
- **Advanced** — [Caching Patterns](https://docs.dsoftstudio.com/mediator/advanced/caching-patterns) · [Pipeline Patterns](https://docs.dsoftstudio.com/mediator/advanced/pipeline-patterns)

---

## Support

❤️ [Sponsor on GitHub](https://github.com/sponsors/yandersr)

---

## License

[MIT](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)