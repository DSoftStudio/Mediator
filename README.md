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

Ultra-low-latency mediator for .NET with compile-time dispatch, zero-allocation pipelines, and a familiar MediatR-style API.

- **~0.6 ns over a direct call** — Send in ~7 ns, only ~0.6 ns above a raw `handler.Handle()` invocation
- **Fastest .NET mediator tested** — ~1.8× faster Send than Mediator (SG), ~5× faster than DispatchR, ~6× faster than MediatR
- **Zero-allocation dispatch** — 72 B per Send (74% less than MediatR), 0 B Publish
- **Auto-Singleton handlers** — stateless handlers (no constructor params) are automatically registered as Singleton, eliminating per-call allocation
- **Compile-time pipeline generation** — source generators discover handlers and precompile pipelines at build time, zero reflection at runtime
- **Native AOT and trimming compatible** — no reflection, `MakeGenericType`, or dynamic code generation in hot paths
- **Familiar developer experience** — drop-in MediatR-style API with `IRequest`, `INotification`, pipeline behaviors, and streaming

## Key Highlights

| Capability | Value |
|---|---|
| Send latency | ~7 ns |
| Publish latency | ~8.5 ns |
| Allocation per Send | 72 B |
| Pipeline overhead | ~0.6 ns over direct call |
| Reflection at runtime | None |
| Native AOT compatible | ✅ |

---

## Execution Model

```
Send(new MyRequest())
  |
  v
Pipeline Behaviors  (logging, validation, transactions...)
  |
  v
Request Handler     (your business logic)
  |
  v
ValueTask<TResponse>
```

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

Register the mediator at startup:

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

> 👉 See the full [Quick Start Guide](docs/getting-started/quick-start.md) and [Installation](docs/getting-started/installation.md) for companion packages.

---

## Why DSoftStudio.Mediator?

| Strength | Detail |
|---|---|
| **Near-direct-call latency** | Send in ~7 ns — only ~0.6 ns above a direct `handler.Handle()` call |
| **Notification speed** | Fastest Publish of any .NET mediator tested (~8.5 ns, zero allocation) |
| **Allocation efficiency** | Zero-alloc Send pipeline (72 B), 74% less than MediatR |
| **Auto-Singleton handlers** | Stateless handlers are automatically Singleton — zero per-call allocation without manual configuration |
| **MediatR compatibility** | Same `IRequest` / `INotification` / `IPipelineBehavior` programming model — minimal migration effort |
| **Compile-time wiring** | Source generators emit dispatch tables at build time — no assembly scanning or reflection at runtime |

---

## Benchmarks (.NET 10)

Tested against [Mediator](https://github.com/martinothamar/Mediator) 3.0.1, [DispatchR](https://github.com/hasanxdev/DispatchR) 2.1.1, and [MediatR](https://github.com/jbogard/MediatR) 14.1.

### Latency

| Operation             | **DSoft**   | Mediator (SG) | DispatchR   | MediatR     |
|-----------------------|------------:|--------------:|------------:|------------:|
| `Send()`              |  **7.1 ns** |      12.5 ns  |    33.4 ns  |    42.1 ns  |
| `Send()` (5 behaviors)| **15.5 ns** |      21.2 ns  |    53.5 ns  |   150.2 ns  |
| `Publish()`           |  **8.5 ns** |      10.2 ns  |    35.0 ns  |   136.1 ns  |
| `CreateStream()`      |     45.8 ns |  **45.3 ns**  |    67.1 ns  |   124.2 ns  |
| Cold Start            | **1.63 µs** |     7.41 µs   |   1.91 µs   |    3.10 µs  |

### Allocations

| Operation             | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|-----------------------|----------:|--------------:|----------:|--------:|
| `Send()`              |    72 B   |        72 B   |    72 B   |   272 B |
| `Send()` (5 behaviors)|    72 B   |        72 B   |    72 B   | 1,088 B |
| `Publish()`           |     0 B   |         0 B   |     0 B   |   768 B |
| `CreateStream()`      |   232 B   |       232 B   |   232 B   |   624 B |

### Feature Comparison

| Feature                   | DSoft | Mediator (SG) | DispatchR | MediatR |
|---------------------------|:----:|:-------------:|:---------:|:-------:|
| Source generators         | ✅ | ✅ | ❌ | ❌ |
| Native AOT compatible     | ✅ | ✅ | ❌ | ❌ |
| Reflection-free hot path  | ✅ | ✅ | ❌ | ❌ |
| Zero-alloc pipeline       | ✅ | ✅ | ✅ | ❌ |
| Auto-Singleton handlers   | ✅ | ❌ | ❌ | ❌ |
| Self-handling requests    | ✅ | ❌ | ❌ | ❌ |
| Runtime-typed `Send(object)` | ✅ | ❌ | ❌ | ✅ |
| Compile-time pipeline     | ✅ | ✅ | ❌ | ❌ |
| MediatR-style API         | ✅ | ✅ | ❌ | ✅ |

> Full BenchmarkDotNet results are available in the [`/benchmarks`](benchmarks) folder.

---

## Features

| Feature | Description | Docs |
|---|---|---|
| Pipeline Behaviors | Zero-allocation behavior chains via interface dispatch | [Docs](docs/features/pipeline-behaviors.md) |
| Pre/Post Processors | Simple before/after hooks without chain responsibility | [Docs](docs/features/pre-post-processors.md) |
| CQRS | `ICommand<T>` / `IQuery<T>` with semantic handler aliases | [Docs](docs/concepts/cqrs.md) |
| Self-Handling Requests | `static Execute` inside request — no separate handler class | [Docs](docs/features/self-handling-requests.md) |
| Notifications | Multi-handler fan-out with pluggable strategies | [Docs](docs/concepts/notifications.md) |
| Runtime Dispatch | `Send(object)` via FrozenDictionary — AOT-safe | [Docs](docs/features/runtime-dispatch.md) |
| Streams | `IAsyncEnumerable<T>` streaming with pipeline support | [Docs](docs/concepts/streams.md) |
| Handler Validation | `ValidateMediatorHandlers()` — fail fast at startup | [Docs](docs/features/handler-validation.md) |
| Native AOT | Full AOT and trimming compatibility | [Docs](docs/architecture/native-aot.md) |

---

## Companion Packages

| Package | Purpose | Docs |
|---|---|---|
| [`DSoftStudio.Mediator.OpenTelemetry`](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry) | Distributed tracing + metrics for Send/Publish/Stream | [Docs](docs/integrations/opentelemetry.md) |
| [`DSoftStudio.Mediator.FluentValidation`](https://www.nuget.org/packages/DSoftStudio.Mediator.FluentValidation) | Automatic request validation via FluentValidation | [Docs](docs/integrations/fluentvalidation.md) |
| [`DSoftStudio.Mediator.HybridCache`](https://www.nuget.org/packages/DSoftStudio.Mediator.HybridCache) | Multi-layer caching (L1 + L2) via Microsoft HybridCache | [Docs](docs/integrations/hybridcache.md) |

---

## Samples

| Sample | Description | Port |
|---|---|---|
| [`basic-api`](samples/basic-api) | Query + Command with Minimal API | 5100 |
| [`pipeline-logging`](samples/pipeline-logging) | LoggingBehavior + ValidationBehavior | 5200 |
| [`domain-events`](samples/domain-events) | INotification with multiple handlers | 5300 |
| [`streaming`](samples/streaming) | IAsyncEnumerable + Server-Sent Events | 5400 |
| [`di-lifetimes`](samples/di-lifetimes) | Transient / Scoped / Singleton registration | 5500 |
| [`pre-post-processors`](samples/pre-post-processors) | IRequestPreProcessor + IRequestPostProcessor | 5600 |
| [`self-handling`](samples/self-handling) | Self-handling requests with static Execute | 5700 |
| [`opentelemetry`](samples/opentelemetry) | Distributed tracing + metrics with OTel console exporter | 5800 |
| [`fluent-validation`](samples/fluent-validation) | FluentValidation integration with ValidationBehavior | 5900 |
| [`caching`](samples/caching) | HybridCache integration with CachingBehavior | 6000 |

```shell
dotnet run --project samples/basic-api/DSoft.Sample.Api
```

---

## Documentation

Full documentation is available in the [`/docs`](docs/index.md) folder:

- **Getting Started** — [Installation](docs/getting-started/installation.md) · [Quick Start](docs/getting-started/quick-start.md) · [Registration Order](docs/getting-started/registration-order.md) · [Migration from MediatR](docs/getting-started/migration-from-mediatr.md)
- **Core Concepts** — [Requests & Handlers](docs/concepts/requests-and-handlers.md) · [Notifications](docs/concepts/notifications.md) · [Streams](docs/concepts/streams.md) · [CQRS](docs/concepts/cqrs.md)
- **Features** — [Pipeline Behaviors](docs/features/pipeline-behaviors.md) · [Pre/Post Processors](docs/features/pre-post-processors.md) · [Self-Handling Requests](docs/features/self-handling-requests.md) · [Runtime Dispatch](docs/features/runtime-dispatch.md) · [Handler Validation](docs/features/handler-validation.md)
- **Integrations** — [OpenTelemetry](docs/integrations/opentelemetry.md) · [FluentValidation](docs/integrations/fluentvalidation.md) · [HybridCache](docs/integrations/hybridcache.md)
- **Architecture** — [Dispatch Pipeline](docs/architecture/dispatch-pipeline.md) · [Source Generators](docs/architecture/source-generators.md) · [Native AOT](docs/architecture/native-aot.md) · [Performance Design](docs/architecture/performance.md)
- **Advanced** — [Caching Patterns](docs/advanced/caching-patterns.md) · [Pipeline Patterns](docs/advanced/pipeline-patterns.md)

---

## Migrating from MediatR

DSoftStudio.Mediator follows MediatR's programming model — migration requires mechanical code changes (namespaces, `Task` → `ValueTask`, behavior signatures) but no architectural rewrite.

**Quick summary of changes:**

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Handler return type | `Task<TResponse>` | `ValueTask<TResponse>` |
| Behavior `next` param | `RequestHandlerDelegate<TResponse>` | `IRequestHandler<TRequest, TResponse>` |
| Calling next | `await next()` | `await next.Handle(request, ct)` |
| Pre/Post processor return | `Task` | `ValueTask` |
| Handler lifetime (default) | All Transient | Stateless → Singleton, with DI deps → Transient |
| Namespace | `using MediatR;` | `using DSoftStudio.Mediator.Abstractions;` |

> 👉 See the complete [step-by-step Migration Guide](docs/getting-started/migration-from-mediatr.md) for detailed instructions with diff examples.

---

## Support

If you find this project useful, consider supporting its development.

❤️ [Sponsor on GitHub](https://github.com/sponsors/yandersr)

---

## License

[MIT License](LICENSE.md)