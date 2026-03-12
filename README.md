# DSoftStudio.Mediator

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![CI](https://github.com/DSoftStudio/Mediator/actions/workflows/ci.yml/badge.svg)](https://github.com/DSoftStudio/Mediator/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=DSoftStudio_Mediator&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=DSoftStudio_Mediator)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
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
- **Native AOT and trimming compatible** — no reflection, `MakeGenericType`, or dynamic code generation in hot paths; ships with `IsAotCompatible` and `IsTrimmable` enabled
- **Familiar developer experience** — drop-in MediatR-style API with `IRequest`, `INotification`, pipeline behaviors, and streaming

DSoftStudio.Mediator follows MediatR's programming model (`IRequest`, `INotification`, `IPipelineBehavior`) with the lowest dispatch latency of any .NET mediator tested — Send in ~7 ns (~0.6 ns overhead vs direct call), Publish in ~8.5 ns with zero allocation. Fully compatible with Native AOT and trimming. Migration from MediatR requires mechanical code changes (namespaces, `Task` → `ValueTask`, behavior signatures) but no architectural rewrite.

---

## Installation

```shell
dotnet add package DSoftStudio.Mediator
```

---

## Quick Start

Define a request and handler:

```csharp
public record Ping() : IRequest<int>;

public class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken ct)
        => new ValueTask<int>(42);
}
```

Register the mediator and precompile pipelines at startup:

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

> **Registration order matters.** The `Precompile*` methods inspect the service collection to determine dispatch strategies and lifetimes. Register all behaviors, processors, exception handlers, notification strategies, and handler lifetime overrides **before** calling `PrecompilePipelines()` / `PrecompileNotifications()` / `PrecompileStreams()`. See [Registration Order](#registration-order) for details.

Send a request:

```csharp
var result = await mediator.Send(new Ping());
```

---

## When to Use DSoftStudio.Mediator

DSoftStudio.Mediator is ideal for:

- **High-throughput APIs** — where every nanosecond of mediator overhead adds up across millions of requests
- **Financial and trading systems** — predictable latency with zero GC pressure from the pipeline
- **Real-time services** — low-latency event dispatch for signaling, notifications, and streaming
- **Event-driven architectures** — decouple producers and consumers with ~8.5 ns zero-allocation Publish
- **Microservices** — ~7 ns / 72 B per dispatch means the mediator layer is negligible even at tens of thousands of requests per second
- **MediatR migrations** — same `IRequest` / `INotification` / `IPipelineBehavior` programming model. Migration requires namespace changes, `Task` → `ValueTask` in handlers, and `next()` → `next.Handle(request, ct)` in behaviors — but no architectural rewrite

| Strength | Detail |
|---|---|
| **Near-direct-call latency** | Send in ~7 ns — only ~0.6 ns above a direct `handler.Handle()` call |
| **Notification speed** | Fastest Publish of any .NET mediator tested (~8.5 ns, zero allocation) |
| **Allocation efficiency** | Zero-alloc Send pipeline (72 B), 74% less than MediatR |
| **Auto-Singleton handlers** | Stateless handlers are automatically Singleton — zero per-call allocation without manual configuration |
| **MediatR compatibility** | Same `IRequest` / `INotification` / `IPipelineBehavior` programming model — minimal migration effort |
| **Compile-time wiring** | Source generators emit dispatch tables at build time — no assembly scanning or reflection at runtime on the primary dispatch paths |

Instead of relying on runtime discovery and reflection, the library uses compile-time generated dispatch tables and precompiled pipelines. The result is predictable low-latency execution with minimal memory pressure across all operations.

---

## Benchmark Summary (.NET 10)

Tested against [Mediator](https://github.com/martinothamar/Mediator) 3.0.1, [DispatchR](https://github.com/AterDev/DispatchR) 2.1.1, and [MediatR](https://github.com/jbogard/MediatR) 14.1.

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

Both DSoft and Mediator (SG) achieve zero-allocation pipelines regardless of behavior count.  
DSoft differentiates on latency: `Send()` is only ~0.6 ns above a direct handler call, making it ~1.8× faster than Mediator (SG), ~5× faster than DispatchR, and ~6× faster than MediatR.

### Feature Comparison

| Feature                   | DSoft | Mediator (SG) | DispatchR | MediatR |
|---------------------------|:----:|:-------------:|:---------:|:-------:|
| Source generators         | ✔️ | ✔️ | ❌ | ❌ |
| Native AOT compatible     | ✔️ | ✔️ | ❌ | ❌ |
| Reflection-free hot path  | ✔️ | ✔️ | ❌ | ❌ |
| Zero-alloc pipeline       | ✔️ | ✔️ | ✔️ | ❌ |
| Auto-Singleton handlers   | ✔️ | ❌ | ❌ | ❌ |
| Compile-time pipeline     | ✔️ | ✔️ | ❌ | ❌ |
| MediatR-style API         | ✔️ | ✔️ | ❌ | ✔️ |

> Full BenchmarkDotNet results are available in the [`/benchmarks`](benchmarks) folder.

---

## Native AOT and Trimming

DSoftStudio.Mediator is fully compatible with .NET Native AOT publishing and IL trimming.

Both packages ship with `IsAotCompatible` and `IsTrimmable` enabled, and the trim analyzer is active at build time. The hot execution path uses no reflection, no `MakeGenericType`, no `Expression.Compile`, and no dynamic method generation — all handler discovery and dispatch wiring are performed at compile time by Roslyn source generators.

This makes the mediator suitable for:

- **Native AOT ASP.NET applications** — publish self-contained, ahead-of-time compiled APIs
- **Serverless / cloud functions** — fast cold start with minimal memory footprint
- **Containerized microservices** — smaller images, no JIT warm-up
- **High-density cloud workloads** — reduced memory per instance

The `Publish(object)` overload (runtime-typed notifications) is also AOT-safe — it uses a compile-time generated `FrozenDictionary<Type, DispatchDelegate>` dispatch table populated by the source generator, with no `MakeGenericType` at runtime.

---

## Features

- Request/response dispatch with `IRequest<TResponse>` and `IRequestHandler<TRequest, TResponse>`
- Pipeline behaviors via `IPipelineBehavior<TRequest, TResponse>`
- Pre/post processing hooks via `IRequestPreProcessor<TRequest>` and `IRequestPostProcessor<TRequest, TResponse>`
- Exception handling via `IRequestExceptionHandler<TRequest, TResponse>`
- Notification publishing via `INotification` and `INotificationHandler<TNotification>`
- Pluggable notification strategies via `INotificationPublisher` (sequential or parallel)
- Async streaming via `IStreamRequest<TResponse>` and `IStreamRequestHandler<TRequest, TResponse>`
- Stream pipeline behaviors via `IStreamPipelineBehavior<TRequest, TResponse>`
- CQRS support with `ICommand<TResponse>`, `IQuery<TResponse>`, `ICommandHandler`, and `IQueryHandler` aliases
- Interface segregation via `ISender` and `IPublisher` for narrower DI injection
- `Unit` type for void-returning commands (`ICommand<Unit>`)
- Compile-time handler discovery (no assembly scanning at runtime)
- Compile-time pipeline precompilation (no lazy initialization on first call)
- Auto-Singleton registration for stateless handlers (no constructor params → Singleton, with DI dependencies → Transient)
- Zero reflection during request execution

---

## Pipeline Behaviors

Pipeline behaviors wrap handler execution, forming a chain:

```
Behavior1 → Behavior2 → … → Handler
```

Each behavior can run logic before and after the next step in the pipeline. The `next` parameter is an `IRequestHandler` — calling `next.Handle(request, ct)` advances the chain via interface dispatch (virtual call) instead of a delegate invocation, enabling **zero-allocation behavior pipelines**.

### Pipeline Allocation

Allocations stay flat at 72 B regardless of pipeline depth:

```mermaid
xychart-beta
    title "Allocation vs Pipeline Depth"
    x-axis "Behaviors" [0, 1, 3, 5]
    y-axis "Bytes" 0 --> 100
    line [72, 72, 72, 72]
```

| Behaviors | DSoft (alloc) | MediatR (alloc) |
|-----------|---------------|-----------------|
| 0         | 72 B          | 272 B           |
| 1         | 72 B          | 544 B           |
| 3         | 72 B          | 800 B           |
| 5         | 72 B          | 1,088 B         |

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        return await next.Handle(request, ct);
    }
}
```

Register behaviors as open generics:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

Behaviors execute in registration order. The first registered behavior is the outermost wrapper.

---

## Pre/Post Processors

For cross-cutting concerns that only need a "before" or "after" hook, pre/post processors are simpler than full pipeline behaviors — no `next` parameter, no chain responsibility.

```
PreProcessor₁ → PreProcessor₂ → Handler → PostProcessor₁ → PostProcessor₂
```

### Pre-Processors

Run before the handler. If a pre-processor throws, the handler is not invoked.

```csharp
public class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken ct)
    {
        // Validate before the handler runs — throw to short-circuit
        if (request is ICommand command)
            Console.WriteLine($"Validating {typeof(TRequest).Name}");

        return ValueTask.CompletedTask;
    }
}
```

### Post-Processors

Run after the handler completes successfully. Not invoked if the handler throws.

```csharp
public class AuditPostProcessor<TRequest, TResponse>
    : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        Console.WriteLine($"{typeof(TRequest).Name} → {response}");
        return ValueTask.CompletedTask;
    }
}
```

Register as open generics:

```csharp
services.AddTransient(typeof(IRequestPreProcessor<>), typeof(ValidationPreProcessor<>));
services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(AuditPostProcessor<,>));
```

---

## CQRS Support (Commands and Queries)

While the mediator can be used directly with `IRequest<TResponse>`, many applications prefer a CQRS-style separation between commands (writes) and queries (reads). `ICommand<TResponse>` and `IQuery<TResponse>` provide semantic clarity while still flowing through the same mediator pipeline.

- `ICommand<TResponse>` represents operations that **modify application state** (writes).
- `IQuery<TResponse>` represents **read-only** operations.
- Both inherit from `IRequest<TResponse>` and flow through the same pipeline.

CQRS markers enable pipeline behaviors to target commands and queries differently — for example, wrapping commands in transactions while applying caching only to queries.

### Commands

Commands express intent to change state. Define a command using `ICommand<TResponse>`. Handlers can implement either `IRequestHandler` or the semantic alias `ICommandHandler`:

```csharp
public record CreateUser(string Name) : ICommand<Guid>;

public class CreateUserHandler : ICommandHandler<CreateUser, Guid>
{
    public ValueTask<Guid> Handle(CreateUser request, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        return new ValueTask<Guid>(id);
    }
}
```

Send a command:

```csharp
var userId = await mediator.Send(new CreateUser("Alice"));
```

### Queries

Queries retrieve data without side effects. Define a query using `IQuery<TResponse>`. Handlers can implement either `IRequestHandler` or the semantic alias `IQueryHandler`:

```csharp
public record GetUser(Guid Id) : IQuery<UserDto>;

public class GetUserHandler : IQueryHandler<GetUser, UserDto>
{
    public ValueTask<UserDto> Handle(GetUser request, CancellationToken ct)
    {
        return new ValueTask<UserDto>(new UserDto(request.Id, "Alice"));
    }
}
```

Send a query:

```csharp
var user = await mediator.Send(new GetUser(userId));
```

### Targeting Behaviors by Message Type

`ICommand` is a non-generic marker interface implemented by all commands. It allows pipeline behaviors to detect write operations at runtime without requiring open generic pattern matching.

Pipeline behaviors can inspect the request type at runtime using the non-generic marker interfaces `ICommand` and `IQuery`:

```csharp
public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        if (request is ICommand)
        {
            // begin transaction
        }

        return await next.Handle(request, ct);
    }
}
```

This pattern enables clean separation of cross-cutting concerns:

- **Transactions** — wrap only commands
- **Caching** — apply only to queries
- **Authorization** — enforce different policies per message type
- **Audit logging** — log writes separately from reads

---

## Notifications

Notifications are dispatched to all registered handlers sequentially. Multiple handlers can subscribe to the same notification type, enabling decoupled event-driven architectures.

```csharp
public record UserCreated(Guid Id) : INotification;

public class SendWelcomeEmail : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken ct)
    {
        Console.WriteLine($"Sending welcome email to {notification.Id}");
        return Task.CompletedTask;
    }
}

public class AuditUserCreation : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken ct)
    {
        Console.WriteLine($"Audit: user {notification.Id} created");
        return Task.CompletedTask;
    }
}
```

Publish a notification:

```csharp
await mediator.Publish(new UserCreated(userId));
```

Both `SendWelcomeEmail` and `AuditUserCreation` will execute in registration order. Handlers are resolved from DI, so each can have its own dependencies.

### Notification Strategies

By default, handlers run **sequentially** in registration order (if one throws, the rest are skipped). To run all handlers **in parallel**, register the built-in `ParallelNotificationPublisher`:

```csharp
services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();
```

| Strategy | Behavior |
|---|---|
| Sequential (default) | Handlers run one at a time. If a handler throws, subsequent handlers are not invoked. |
| `ParallelNotificationPublisher` | All handlers start concurrently via `Task.WhenAll`. If any throw, an `AggregateException` is raised after all complete. |

You can also implement `INotificationPublisher` for custom strategies (fire-and-forget, batched, prioritized, etc.):

```csharp
public class FireAndForgetPublisher : INotificationPublisher
{
    public Task Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        foreach (var handler in handlers)
            _ = handler.Handle(notification, cancellationToken);

        return Task.CompletedTask;
    }
}
```

---

## Streams

Async streaming returns an `IAsyncEnumerable<T>` from a handler. This is useful for large datasets, event feeds, and progressive responses where buffering the entire result set in memory is impractical.

```csharp
public record StreamNumbers() : IStreamRequest<int>;

public class StreamNumbersHandler
    : IStreamRequestHandler<StreamNumbers, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamNumbers request,
        CancellationToken ct)
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
```

Consume the stream:

```csharp
await foreach (var n in mediator.CreateStream(new StreamNumbers()))
{
    Console.WriteLine(n);
}
```

Stream handlers support cancellation through the `CancellationToken` parameter and can be combined with `IStreamPipelineBehavior<TRequest, TResponse>` for cross-cutting concerns like logging or rate limiting.

---

## Samples

The repository includes working samples demonstrating common usage patterns:

| Sample | Description | Port |
|---|---|---|
| [`basic-api`](samples/basic-api) | Query + Command with Minimal API | 5100 |
| [`pipeline-logging`](samples/pipeline-logging) | LoggingBehavior + ValidationBehavior | 5200 |
| [`domain-events`](samples/domain-events) | INotification with multiple handlers | 5300 |
| [`streaming`](samples/streaming) | IAsyncEnumerable + Server-Sent Events | 5400 |
| [`di-lifetimes`](samples/di-lifetimes) | Transient / Scoped / Singleton registration | 5500 |
| [`pre-post-processors`](samples/pre-post-processors) | IRequestPreProcessor + IRequestPostProcessor | 5600 |

Run any sample:

```shell
dotnet run --project samples/basic-api/DSoft.Sample.Api
```

---

## Migrating from MediatR

This guide targets **MediatR 12–14.x** (the current `MediatR` unified package). Earlier versions that used separate `MediatR.Extensions.Microsoft.DependencyInjection` follow the same steps.

### 1. Replace the NuGet package

```shell
dotnet remove package MediatR
dotnet add package DSoftStudio.Mediator
```

### 2. Update namespaces

```diff
- using MediatR;
+ using DSoftStudio.Mediator.Abstractions;
+ using DSoftStudio.Mediator;
```

### 3. Update service registration

```diff
- services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
+ services
+     .AddMediator()
+     .RegisterMediatorHandlers()
+     .PrecompilePipelines()
+     .PrecompileNotifications()
+     .PrecompileStreams();
```

### 4. Update handler return types

```diff
  public class PingHandler : IRequestHandler<Ping, int>
  {
-     public Task<int> Handle(Ping request, CancellationToken ct)
-         => Task.FromResult(42);
+     public ValueTask<int> Handle(Ping request, CancellationToken ct)
+         => new ValueTask<int>(42);
  }
```

> Notification handlers stay the same — both use `Task Handle(...)`.

### 5. Update pipeline behavior signatures

```diff
  public class LoggingBehavior<TRequest, TResponse>
      : IPipelineBehavior<TRequest, TResponse>
      where TRequest : IRequest<TResponse>
  {
-     public async Task<TResponse> Handle(
+     public async ValueTask<TResponse> Handle(
          TRequest request,
-         RequestHandlerDelegate<TResponse> next,
+         IRequestHandler<TRequest, TResponse> next,
          CancellationToken ct)
      {
          Console.WriteLine($"Handling {typeof(TRequest).Name}");
-         return await next();
+         return await next.Handle(request, ct);
      }
  }
```

### 6. Update Send calls (usually no change needed)

The source generator creates typed extension methods, so `mediator.Send(new Ping())` works out of the box with full type inference — zero reflection, best performance:

```csharp
// Works automatically via generated extension method (recommended)
var result = await mediator.Send(new Ping());

// Explicit generics also work
var result = await mediator.Send<Ping, int>(new Ping());
```

### 7. Update Pre/Post processor return types

Pre/post processors use `ValueTask` instead of MediatR’s `Task`:

```diff
  public class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
  {
-     public Task Process(TRequest request, CancellationToken ct)
+     public ValueTask Process(TRequest request, CancellationToken ct)
      {
          // ...
-         return Task.CompletedTask;
+         return ValueTask.CompletedTask;
      }
  }
```

### 8. Handler lifetimes (behavioral difference)

MediatR registers all handlers as **Transient** by default. DSoftStudio.Mediator uses **automatic lifetime detection**:

- **Stateless handlers** (no constructor parameters) → **Singleton** (zero allocation per call)
- **Handlers with DI dependencies** → **Transient** (safe default)

You can override any handler’s lifetime after `RegisterMediatorHandlers()` — the last registration wins. Place overrides **before** `PrecompilePipelines()` so the pipeline chain picks up the correct lifetime:

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers();

// Override before PrecompilePipelines so the chain lifetime is correct
services.AddScoped<IRequestHandler<MyRequest, MyResponse>, MyHandler>();

services
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

### What stays the same

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Request marker | `IRequest<TResponse>` | `IRequest<TResponse>` |
| Notification marker | `INotification` | `INotification` |
| Notification handler | `Task Handle(T, CancellationToken)` | `Task Handle(T, CancellationToken)` |
| Stream request | `IStreamRequest<TResponse>` | `IStreamRequest<TResponse>` |
| Send syntax | `mediator.Send(new Ping())` | `mediator.Send(new Ping())` (via generated extension) |
| Open generic registration | `services.AddTransient(typeof(IPipelineBehavior<,>), ...)` | Same |

### What changes

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Handler return type | `Task<TResponse>` | `ValueTask<TResponse>` |
| Behavior `next` param | `RequestHandlerDelegate<TResponse>` | `IRequestHandler<TRequest, TResponse>` |
| Calling next | `await next()` | `await next.Handle(request, ct)` |
| Pre/Post processor return | `Task` | `ValueTask` |
| Handler lifetime (default) | All Transient | Stateless → Singleton, with DI deps → Transient |
| Namespace | `using MediatR;` | `using DSoftStudio.Mediator.Abstractions;` |

---

## Registration Order

The `Precompile*` methods inspect the `IServiceCollection` at startup to determine dispatch strategies and chain lifetimes. **All service registrations must happen before the corresponding `Precompile*` call.**

```csharp
services
    .AddMediator()                // 1. Core mediator services
    .RegisterMediatorHandlers();  // 2. Source-generated handler registrations

// 3. Register behaviors, processors, exception handlers
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IRequestPreProcessor<>), typeof(ValidationPreProcessor<>));
services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(AuditPostProcessor<,>));

// 4. Override handler lifetimes (optional)
services.AddScoped<IRequestHandler<MyRequest, MyResponse>, MyHandler>();

// 5. Register notification strategies (optional)
services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();

// 6. Precompile — inspects all registrations above
services
    .PrecompilePipelines()        // scans for IPipelineBehavior, Pre/Post processors, exception handlers
    .PrecompileNotifications()    // builds static dispatch arrays for each INotification type
    .PrecompileStreams();         // builds static factory delegates for each IStreamRequest type
```

| Method | What it inspects | What to register before |
|---|---|---|
| `PrecompilePipelines()` | `IPipelineBehavior<,>`, `IRequestPreProcessor<>`, `IRequestPostProcessor<,>`, `IRequestExceptionHandler<,>`, handler lifetimes | Behaviors, processors, exception handlers, handler overrides |
| `PrecompileNotifications()` | `INotificationHandler<>` | Notification handler overrides |
| `PrecompileStreams()` | `IStreamRequestHandler<,>` | Stream handler overrides |

`PrecompilePipelines()` determines each `PipelineChainHandler` lifetime based on the registered components: **Singleton** when all components are Singleton, **Scoped** when any is Scoped, **Transient** when any is Transient. Registrations added after the `Precompile*` calls will not be picked up by the dispatch tables.

---

## Architecture

Service resolution goes directly through `IServiceProvider` — the standard DI container call that every mediator must make. This avoids additional container abstractions or service locators.

> **Note:** Backward-compatible overloads (`Send<TResponse>(IRequest<TResponse>)`, `Publish(object)`, and `CreateStream`) use a one-time reflection + `ConcurrentDictionary` cache on first call per type. Subsequent calls hit the cache with zero reflection. Prefer the strongly-typed overloads for maximum performance.

### Runtime Execution Path

The hot path for `Send()` resolves a transient `PipelineChainHandler` from DI:

```
Mediator.Send<TRequest, TResponse>()
   │
   ▼
serviceProvider.GetRequiredService<PipelineChainHandler<TRequest, TResponse>>()
   │
   ▼
[no behaviors] → handler.Handle()                 ← fast path: direct call
[N behaviors]  → PipelineChainHandler (index++)   ← zero-alloc: interface dispatch, no closures
```

**What is registered at startup:**
- **`RegisterMediatorHandlers()`** — registers handlers with automatic lifetime selection: **Singleton** for stateless handlers (no constructor parameters), **Transient** for handlers with DI dependencies. This eliminates per-call allocation for stateless handlers while preserving correct DI semantics for handlers that inject services.
- **`PrecompilePipelines()`** — registers `PipelineChainHandler<TRequest, TResponse>` for every request type that has pipeline components (behaviors, pre/post processors, exception handlers). The chain’s lifetime is determined by its components: Singleton when all are Singleton, Scoped when any is Scoped, Transient when any is Transient.
- **`PrecompileNotifications()`** — populates `NotificationDispatch<T>.Handlers` static arrays with factory delegates for each notification type.
- **`PrecompileStreams()`** — populates `StreamDispatch<TRequest, TResponse>.Handler` static factory delegates for each stream type.

**What runs per request:**
- For requests **without behaviors**, the interceptor resolves the handler directly from DI — Singleton handlers return the cached instance (zero allocation), Transient handlers create a new instance
- For requests **with behaviors**, a `PipelineChainHandler` is resolved from DI — it passes itself as the `next` parameter to each behavior via interface dispatch, advancing through the behavior array without allocating closures or delegates
- Pre/post processors and exception handlers execute around the core pipeline when registered

This design ensures correct lifetime semantics: Singleton handlers are shared across all calls (safe for stateless handlers), Transient handlers get new instances per call (safe for handlers with DI dependencies), and Scoped handlers are shared within the HTTP request scope. Users can always override the auto-detected lifetime by re-registering after `RegisterMediatorHandlers()` — the last registration wins.

### Source Generators

Four Roslyn incremental source generators handle all discovery and wiring at build time:

| Generator | Output | Purpose |
|---|---|---|
| `DependencyInjectionGenerator` | `MediatorServiceRegistry.g.cs` | Scans for `IRequestHandler`, `INotificationHandler`, and `IStreamRequestHandler` implementations. Registers stateless handlers (no constructor params) as **Singleton**, others as Transient |
| `MediatorPipelineGenerator` | `MediatorRegistry.g.cs` | Registers `PipelineChainHandler<TRequest, TResponse>` as transient for every request type via `PrecompilePipelines()` |
| `NotificationGenerator` | `NotificationRegistry.g.cs` | Generates static dispatch arrays for each notification type, eliminating runtime service enumeration |
| `StreamGenerator` | `StreamRegistry.g.cs` | Generates static factory delegates for each stream handler |

### What This Means at Runtime

- **Handler discovery** is done at compile time — no assembly scanning, no `GetTypes()`, no attribute reflection.
- **Request dispatch** resolves a new `PipelineChainHandler` from DI per `Send()` call. The handler, behaviors, pre/post processors, and exception handlers are injected by the container.
- **Notification dispatch** reads a precompiled `Func<IServiceProvider, INotificationHandler<T>>[]` array — no `GetServices<T>()` enumeration per publish.
- **Stream dispatch** resolves handlers through a precompiled factory delegate stored in `StreamDispatch<TRequest, TResponse>.Handler`.

The result is that every `Send()`, `Publish()`, and `CreateStream()` call at runtime uses precompiled dispatch tables with zero discovery overhead. Stateless handlers are Singleton — the DI container returns the cached instance with zero allocation. Handlers with DI dependencies remain Transient for correct lifetime semantics. Notification handlers are resolved per publish via factory delegates.

---

## Support

If you find this project useful, consider supporting its development.

❤️ [Sponsor on GitHub](https://github.com/sponsors/yandersr)

---

## License

[MIT License](LICENSE.md)