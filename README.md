# DSoftStudio.Mediator

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DSoftStudio.Mediator.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator)
[![CI](https://github.com/DSoftStudio/DSoftStudio.Mediator/actions/workflows/ci.yml/badge.svg)](https://github.com/DSoftStudio/DSoftStudio.Mediator/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=DSoftStudio_DSoftStudio.Mediator&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=DSoftStudio_DSoftStudio.Mediator)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
![Publish](https://img.shields.io/badge/publish-18ns-brightgreen)
![Send](https://img.shields.io/badge/send-18ns-blue)
![Alloc](https://img.shields.io/badge/alloc-72B-orange)

High-performance mediator for .NET with compile-time pipeline generation, zero-allocation dispatch, and a familiar MediatR-style API.

- **Fastest Send and Publish** — Send in 18 ns (2× faster than DispatchR), Publish in 18 ns (~2× faster than DispatchR, ~7× faster than MediatR)
- **Zero-allocation dispatch** — 72 B per Send (same as DispatchR, 74% less than MediatR), 0 B Publish
- **Auto-Singleton handlers** — stateless handlers (no constructor params) are automatically registered as Singleton, eliminating per-call allocation
- **Compile-time pipeline generation** — source generators discover handlers and precompile pipelines at build time, zero reflection at runtime
- **Familiar developer experience** — drop-in MediatR-style API with `IRequest`, `INotification`, pipeline behaviors, and streaming

DSoftStudio.Mediator follows MediatR's programming model (`IRequest`, `INotification`, `IPipelineBehavior`) with the fastest Send (18 ns) and Publish (18 ns), 2× faster than DispatchR and up to 7× faster than MediatR — with 74% lower allocations. Migration requires mechanical code changes (namespaces, `Task` → `ValueTask`, behavior signatures) but no architectural rewrite.

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

Send a request:

```csharp
var result = await mediator.Send<Ping, int>(new Ping());
```

---

## When to Use DSoftStudio.Mediator

DSoftStudio.Mediator is ideal for:

- **High-throughput APIs** — where every nanosecond of mediator overhead adds up across millions of requests
- **Financial and trading systems** — predictable latency with zero GC pressure from the pipeline
- **Real-time services** — low-latency event dispatch for signaling, notifications, and streaming
- **Event-driven architectures** — decouple producers and consumers with 18 ns Publish dispatch
- **Microservices** — 18 ns / 72 B per dispatch means the mediator layer is negligible even at tens of thousands of requests per second
- **MediatR migrations** — same `IRequest` / `INotification` / `IPipelineBehavior` programming model. Migration requires namespace changes, `Task` → `ValueTask` in handlers, and `next()` → `next.Handle(request, ct)` in behaviors — but no architectural rewrite

| Strength | Detail |
|---|---|
| **Notification speed** | Fastest Publish of any .NET mediator tested (18 ns, zero allocation) |
| **Allocation efficiency** | Zero-alloc Send pipeline (72 B), on par with DispatchR and 74% less than MediatR |
| **Auto-Singleton handlers** | Stateless handlers are automatically Singleton — zero per-call allocation without manual configuration |
| **MediatR compatibility** | Same `IRequest` / `INotification` / `IPipelineBehavior` programming model — minimal migration effort |
| **Compile-time wiring** | Source generators emit dispatch tables at build time — no assembly scanning or reflection at runtime on the primary dispatch paths |

Instead of relying on runtime discovery and reflection, the library uses compile-time generated dispatch tables and precompiled pipelines. The result is predictable low-latency execution with minimal memory pressure across all operations.

---

## Benchmark Summary (.NET 10)

Tested against MediatR 14.1 and DispatchR 2.1.1.

| Operation  | DSoft    | DispatchR  | MediatR   |
|----------- |---------:|-----------:|----------:|
| Send       | 18.0 ns  | 34.5 ns    | 46.8 ns   |
| Publish    | 18.4 ns  | 35.2 ns    | 124.3 ns  |
| Stream     | 56.4 ns  | 68.3 ns    | 123.1 ns  |
| Cold Start | 1.62 µs  | 1.80 µs    | 3.11 µs   |

DSoft leads every category. Send is 2× faster than DispatchR and 2.6× faster than MediatR. Publish is zero-allocation.

> Full BenchmarkDotNet results are available in the [`/benchmarks`](benchmarks) folder.

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

| Behaviors | DSoft (alloc)|
|-----------|--------------|
| 0         | 72 B         |
| 1         | 72 B         |
| 3         | 72 B         |
| 5         | 72 B         |

Allocations stay flat at 72 B regardless of pipeline depth.

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
var userId = await mediator.Send<CreateUser, Guid>(new CreateUser("Alice"));
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
var user = await mediator.Send<GetUser, UserDto>(new GetUser(userId));
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

You can override any handler’s lifetime after `RegisterMediatorHandlers()` — the last registration wins:

```csharp
// Force a specific handler to Transient (if needed)
services.AddTransient<IRequestHandler<MyRequest, MyResponse>, MyHandler>();
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

## License

[MIT License](LICENSE.md)