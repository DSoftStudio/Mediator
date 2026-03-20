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

> 👉 See the full [Quick Start Guide](https://docs.dsoftstudio.com/mediator/getting-started/quick-start) and [Installation](https://docs.dsoftstudio.com/mediator/getting-started/installation) for companion packages.

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
| Pipeline Behaviors | Zero-allocation behavior chains via interface dispatch | [Docs](https://docs.dsoftstudio.com/mediator/features/pipeline-behaviors) |
| Pre/Post Processors | Simple before/after hooks without chain responsibility | [Docs](https://docs.dsoftstudio.com/mediator/features/pre-post-processors) |
| CQRS | `ICommand<T>` / `IQuery<T>` with semantic handler aliases | [Docs](https://docs.dsoftstudio.com/mediator/concepts/cqrs) |
| Self-Handling Requests | `static Execute` inside request — no separate handler class | [Docs](https://docs.dsoftstudio.com/mediator/features/self-handling-requests) |
| Notifications | Multi-handler fan-out with pluggable strategies | [Docs](https://docs.dsoftstudio.com/mediator/concepts/notifications) |
| Runtime Dispatch | `Send(object)` via FrozenDictionary — AOT-safe | [Docs](https://docs.dsoftstudio.com/mediator/features/runtime-dispatch) |
| Streams | `IAsyncEnumerable<T>` streaming with pipeline support | [Docs](https://docs.dsoftstudio.com/mediator/concepts/streams) |
| Handler Validation | `ValidateMediatorHandlers()` — fail fast at startup | [Docs](https://docs.dsoftstudio.com/mediator/features/handler-validation) |
| Native AOT | Full AOT and trimming compatibility | [Docs](https://docs.dsoftstudio.com/mediator/architecture/native-aot) |

---

## Companion Packages

| Package | Purpose | Docs |
|---|---|---|
| [`DSoftStudio.Mediator.OpenTelemetry`](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry) | Distributed tracing + metrics for Send/Publish/Stream | [Docs](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry) |
| [`DSoftStudio.Mediator.FluentValidation`](https://www.nuget.org/packages/DSoftStudio.Mediator.FluentValidation) | Automatic request validation via FluentValidation | [Docs](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation) |
| [`DSoftStudio.Mediator.HybridCache`](https://www.nuget.org/packages/DSoftStudio.Mediator.HybridCache) | Multi-layer caching (L1 + L2) via Microsoft HybridCache | [Docs](https://docs.dsoftstudio.com/mediator/integrations/hybridcache) |

---

## Design Notes

### Interceptor code generation (Release vs Debug)

The source generators emit C# [interceptors](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12#interceptors) that replace `ISender.Send`, `IPublisher.Publish`, and `IMediator.CreateStream` call sites at compile time with direct pipeline invocations — eliminating virtual dispatch entirely.

The generated code adapts to the build's **`OptimizationLevel`**:

| Build | Generated pattern | Overhead vs direct call | Mock-safe |
|---|---|---|---|
| **Release** | Branchless `castclass IServiceProviderAccessor` | ~0.6 ns | ❌ throws `InvalidCastException` on mocks |
| **Debug** | `is not IServiceProviderAccessor` + virtual fallback | ~3 ns | ✅ graceful fallback to virtual dispatch |

**Why?** A single `isinst` + branch instruction prevents the JIT's Guarded Devirtualization (GDV) from fully devirtualizing the interface call, adding ~3 ns on every invocation. The branchless `castclass` pattern in Release lets GDV optimize the dispatch to a method-table pointer comparison + direct field load — effectively zero overhead.

In **Debug** builds (where tests typically run), the generated interceptors detect test doubles (Moq, NSubstitute, etc.) that don't implement `IServiceProviderAccessor` and fall back to virtual dispatch, so mocked `ISender`/`IMediator` instances work as expected.

> **Tip:** If you run tests in Release mode and mock `ISender` in a project that references the generator, the interceptor will throw `InvalidCastException`. Either remove the generator reference from pure unit-test projects or use the strongly-typed mock setup. See the **Suppressing interceptors** section below.

### Suppressing interceptors (`DSoftMediatorSuppressInterceptors`)

If your test project references `DSoftStudio.Mediator` directly (not just `Abstractions`) **and** mocks `ISender`/`IPublisher`/`IMediator`, you can disable interceptor generation entirely with an MSBuild property:

```xml
<PropertyGroup>
  <DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors>
</PropertyGroup>
```

When set to `true`:
- The `.props` auto-import stops adding the interceptor namespace, so the C# compiler will not recognize `[InterceptsLocation]` attributes.
- All three source generators (`Send`, `Publish`, `Stream`) read the property via `AnalyzerConfigOptionsProvider` and skip code emission entirely.
- Your project falls back to standard virtual dispatch through the `IMediator` / `ISender` interfaces — fully mock-safe at any optimization level.

### DSOFT004 — Mocking library detected with interceptors enabled

The generator package includes an incremental analyzer that scans `ReferencedAssemblyNames` for well-known mocking libraries:

> Moq · NSubstitute · FakeItEasy · Telerik.JustMock · RhinoMocks · NimbleMocks

If any of these are referenced **and** `DSoftMediatorSuppressInterceptors` is not `true`, the build emits:

```
warning DSOFT004: This project references mocking library 'Moq' and has interceptors enabled.
In Release builds, interceptors use a branchless cast that throws InvalidCastException on mock
objects. Either reference only DSoftStudio.Mediator.Abstractions in test projects, or set
<DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors> in this project.
```

**Recommended patterns:**

| Test project setup | Interceptors | Mock-safe | Action needed |
|---|---|---|---|
| References only `Abstractions` | Not generated | ✅ | None (preferred) |
| References `Mediator` + `SuppressInterceptors=true` | Suppressed | ✅ | Add MSBuild property |
| References `Mediator` + Debug build | Generated (isinst fallback) | ✅ | None |
| References `Mediator` + Release build | Generated (castclass) | ❌ | Suppress or restructure |

### Abstractions-only project pattern (recommended)

The recommended multi-project architecture separates the generator host from your domain/application layer:

```
Host (Web API / Worker)
├── References: DSoftStudio.Mediator (includes source generators)
├── References: Host.Application
│
Host.Application (Domain / Application layer)
├── References: DSoftStudio.Mediator.Abstractions (interfaces only)
├── Contains: IRequest<T> commands, IRequestHandler<,> implementations
│
Host.Tests (Unit tests)
├── References: DSoftStudio.Mediator.Abstractions (interfaces only)
├── Mocks: ISender / IPublisher via Moq, NSubstitute, etc.
```

**Why this works:**

1. **Source generators run in `Host`** — they discover handlers from `Host.Application` via the `ReferencedAssemblyScanner` Phase 2 (type-based fallback), which walks all exported types in assemblies that reference Abstractions.
2. **Typed extensions are generated** — e.g. `sender.Send(new RunTaskCommand())` compiles to `sender.Send<RunTaskCommand, int>(request, ct)` via pure virtual dispatch. No `IServiceProviderAccessor` cast.
3. **Test projects are fully mock-safe** — since they only reference `Abstractions`, no interceptors are generated. Mocking `ISender` works with any test double framework.

```csharp
// Host.Application — only references Abstractions
public sealed record RunTaskCommand(string Name) : IRequest<int>;

public sealed class RunTaskCommandHandler : IRequestHandler<RunTaskCommand, int>
{
    public ValueTask<int> Handle(RunTaskCommand request, CancellationToken ct) => new(42);
}

// Host.Tests — mocks ISender freely, no generator interference
var sender = new Mock<ISender>();
sender.Setup(s => s.Send<RunTaskCommand, int>(It.IsAny<RunTaskCommand>(), default))
      .ReturnsAsync(42);
```

> **Note:** The `Send(object)` runtime dispatch overload requires the real `Mediator` instance (it needs `IServiceProviderAccessor`). If you call `sender.Send((object)request)` on a mock, you'll get a helpful `InvalidOperationException` guiding you to use the typed overload instead.

### Notification dispatch by exact type

Notification handlers are dispatched by **exact compile-time type**, not by runtime inheritance hierarchy. This is a deliberate design decision:

```csharp
// Only handlers registered for OrderPlaced are invoked —
// handlers for INotification or a base class are NOT invoked.
await mediator.Publish(new OrderPlaced(orderId));
```

This avoids the [MediatR duplicate-handler bug](https://github.com/jbogard/MediatR/issues) where a single notification can trigger base-class handlers unexpectedly, and enables the source generator to emit a static dispatch table at build time with zero reflection.

### Publish interceptor — `NotificationPublisherFlag` bypass

Most applications never register a custom `INotificationPublisher`. Without optimization, every generated `Publish` interceptor would call `GetService<INotificationPublisher>()` on every invocation — even when the result is always `null`. That DI probe alone costs ~3–4 ns per call.

`NotificationPublisherFlag` is a write-once global `Volatile` boolean that eliminates this probe:

1. **Default path (no custom publisher):** The flag is `false`. The generated interceptor reads `HasCustomPublisher` (~0.1 ns) and short-circuits directly to `NotificationCachedDispatcher.DispatchSequential` — zero DI lookup.
2. **Custom publisher path:** When `INotificationPublisher` is registered (e.g. `ParallelNotificationPublisher`, OpenTelemetry's `InstrumentedNotificationPublisher`), the `Mediator` constructor calls `MarkRegistered()` once. All subsequent interceptors see the flag and take the `GetService` path as before.

| Scenario | Before optimization | After optimization |
|---|---|---|
| No custom publisher (default) | `GetService` returns `null` (~3–4 ns) | `Volatile.Read` (~0.1 ns) |
| Custom publisher registered | `GetService` returns instance (~3–4 ns) | `Volatile.Read` + `GetService` (~3–4 ns) |

Net effect: **~4 ns saved per `Publish` call** in the default (no custom publisher) path — bringing the Publish interceptor from ~2.2× to ~1.1× overhead vs direct dispatch.

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
| [`mocking`](samples/mocking) | Expression tree detection + mock setup patterns | — |
| [`cross-project-mocking`](samples/cross-project-mocking) | Recommended 3-project architecture for testability | — |

```shell
dotnet run --project samples/basic-api/DSoft.Sample.Api
```

---

## Documentation

Full documentation is available at [docs.dsoftstudio.com/mediator](https://docs.dsoftstudio.com/mediator):

- **Getting Started** — [Installation](https://docs.dsoftstudio.com/mediator/getting-started/installation) · [Quick Start](https://docs.dsoftstudio.com/mediator/getting-started/quick-start) · [Registration Order](https://docs.dsoftstudio.com/mediator/getting-started/registration-order) · [Migration from MediatR](https://docs.dsoftstudio.com/mediator/getting-started/migration-from-mediatr)
- **Core Concepts** — [Requests & Handlers](https://docs.dsoftstudio.com/mediator/concepts/requests-and-handlers) · [Notifications](https://docs.dsoftstudio.com/mediator/concepts/notifications) · [Streams](https://docs.dsoftstudio.com/mediator/concepts/streams) · [CQRS](https://docs.dsoftstudio.com/mediator/concepts/cqrs)
- **Features** — [Pipeline Behaviors](https://docs.dsoftstudio.com/mediator/features/pipeline-behaviors) · [Pre/Post Processors](https://docs.dsoftstudio.com/mediator/features/pre-post-processors) · [Self-Handling Requests](https://docs.dsoftstudio.com/mediator/features/self-handling-requests) · [Runtime Dispatch](https://docs.dsoftstudio.com/mediator/features/runtime-dispatch) · [Handler Validation](https://docs.dsoftstudio.com/mediator/features/handler-validation)
- **Integrations** — [OpenTelemetry](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry) · [FluentValidation](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation) · [HybridCache](https://docs.dsoftstudio.com/mediator/integrations/hybridcache)
- **Architecture** — [Dispatch Pipeline](https://docs.dsoftstudio.com/mediator/architecture/dispatch-pipeline) · [Source Generators](https://docs.dsoftstudio.com/mediator/architecture/source-generators) · [Native AOT](https://docs.dsoftstudio.com/mediator/architecture/native-aot) · [Performance Design](https://docs.dsoftstudio.com/mediator/architecture/performance)
- **Advanced** — [Caching Patterns](https://docs.dsoftstudio.com/mediator/advanced/caching-patterns) · [Pipeline Patterns](https://docs.dsoftstudio.com/mediator/advanced/pipeline-patterns)

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

> 👉 See the complete [step-by-step Migration Guide](https://docs.dsoftstudio.com/mediator/getting-started/migration-from-mediatr) for detailed instructions with diff examples.

---

## Support

If you find this project useful, consider supporting its development.

❤️ [Sponsor on GitHub](https://github.com/sponsors/yandersr)

---

## License

[MIT License](LICENSE.md)