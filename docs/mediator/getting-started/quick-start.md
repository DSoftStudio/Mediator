---
layout: default
title: "Quick Start - DSoftStudio.Mediator"
description: "Get started with DSoftStudio.Mediator in 5 minutes."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Quick Start

## 1. Define a Request and Handler

```csharp
public record Ping() : IRequest<int>;

public class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken ct)
        => new ValueTask<int>(42);
}
```

## 2. Register at Startup

### Recommended: Single-call registration (v1.2.0+)

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
> Without the callback, `AddMediator()` only registers core services and you must complete
> the setup manually (see step-by-step registration below).

`AddMediator(configure)` is a single entry point that automatically:
1. Registers core mediator services (`IMediator`, `ISender`, `IPublisher`)
2. Discovers and registers all handlers across referenced projects
3. Applies your pipeline configuration via the builder callback
4. Precompiles dispatch pipelines and freezes dispatch tables

Available builder methods:

| Method | Purpose |
|---|---|
| `AddOpenBehavior(Type, ServiceLifetime)` | Open-generic `IPipelineBehavior<,>` |
| `AddStreamBehavior<T>(ServiceLifetime)` | Closed `IStreamPipelineBehavior<,>` |
| `AddRequestPreProcessor<T>(ServiceLifetime)` | `IRequestPreProcessor<T>` |
| `AddRequestPostProcessor<T>(ServiceLifetime)` | `IRequestPostProcessor<T,R>` |
| `AddRequestExceptionHandler<T>(ServiceLifetime)` | `IRequestExceptionHandler<T,R>` |
| `AddParallelNotificationPublisher()` | Replace sequential with `Task.WhenAll` dispatch |

### Alternative: Step-by-step registration (v1.1.x style)

If you omit the builder callback, `AddMediator()` only registers core services (`IMediator`, `ISender`, `IPublisher`). You must chain the remaining steps yourself:

```csharp
services
    .AddMediator()                    // Core services only
    .RegisterMediatorHandlers();      // Discover and register all handlers

// Register behaviors, processors, etc.
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

services
    .PrecompilePipelines()            // Build dispatch table and freeze
    .PrecompileNotifications()
    .PrecompileStreams();
```

This pattern remains fully supported for advanced scenarios where you need to insert registrations between steps.

> **Summary**
>
> | Pattern | Call | What it does |
> |---|---|---|
> | **Builder (recommended)** | `services.AddMediator(builder => { ... })` or `services.AddMediator(_ => { })` | Everything: core + handlers + builder config + precompile + freeze |
> | **Manual (v1.1.x)** | `services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines()` | User controls each step |

> **Do not mix both approaches.** Using `AddMediator(configure)` together with `RegisterMediatorHandlers()` or `PrecompilePipelines()` causes redundant registrations. The compile-time diagnostic **DSOFT007** will warn you if mixed usage is detected. See [Registration Order](registration-order.md) for details.

> **Cross-project handler discovery.** Both approaches automatically discover
> all `IRequestHandler<,>`, `INotificationHandler<>`, and `IStreamRequestHandler<,>` implementations
> across **all referenced projects** — no manual registration required. Each project that references
> `DSoftStudio.Mediator` emits `[assembly: MediatorHandlerRegistration]` attributes at compile time,
> and downstream projects read them to build the complete handler registry.
> This works for Clean Architecture setups where handlers live in Application or Infrastructure
> layers and the host/API project only calls `RegisterMediatorHandlers()` (or `AddMediator(configure)`).

## 3. Send a Request

```csharp
var result = await mediator.Send(new Ping());
```

> **Required namespaces** — which `using` directives you need depends on the project layer:
>
> | Project layer | Namespace | Why |
> |---|---|---|
> | Domain / Application (handlers, requests) | `DSoftStudio.Mediator.Abstractions` | Interfaces: `IRequest<T>`, `ICommand<T>`, `ISender`, etc. |
> | Host / API (startup, DI) | `DSoftStudio.Mediator.Abstractions` **+** `DSoftStudio.Mediator` | Adds typed `Send()` / `CreateStream()` extensions and `AddMediator()` DI registration |
>
> The source generator emits typed extension methods (e.g. `Send(Ping)`) in the `DSoftStudio.Mediator` namespace — the host project needs this `using` to call them.
>
> **Tip:** add the namespaces your project needs to a `GlobalUsings.cs` file so every file picks them up automatically.

## Features at a Glance

- Request/response dispatch with `IRequest<TResponse>` and `IRequestHandler<TRequest, TResponse>`
- Pipeline behaviors via `IPipelineBehavior<TRequest, TResponse>`
- Pre/post processing hooks via `IRequestPreProcessor<TRequest>` and `IRequestPostProcessor<TRequest, TResponse>`
- Exception handling via `IRequestExceptionHandler<TRequest, TResponse>`
- Notification publishing via `INotification` and `INotificationHandler<TNotification>`
- Pluggable notification strategies via `INotificationPublisher` (sequential or parallel)
- Async streaming via `IStreamRequest<TResponse>` and `IStreamRequestHandler<TRequest, TResponse>`
- Stream pipeline behaviors via `IStreamPipelineBehavior<TRequest, TResponse>`
- CQRS support with `ICommand<TResponse>`, `IQuery<TResponse>`, `ICommandHandler`, and `IQueryHandler` aliases
- Self-handling requests — place a `static Execute` method inside the request class, no separate handler needed
- Runtime-typed `Send(object)` dispatch for message bus / command queue scenarios — AOT-safe, no reflection
- Interface segregation via `ISender` and `IPublisher` for narrower DI injection
- `Unit` type for void-returning commands (`ICommand<Unit>`)
- Compile-time handler discovery (no assembly scanning at runtime)
- Compile-time pipeline precompilation (no lazy initialization on first call)
- Auto-Singleton registration for stateless handlers (no constructor params → Singleton, with DI dependencies → Transient)
- Zero reflection during request execution
- Fail-fast handler validation via `ValidateMediatorHandlers()` — detect misconfigured handlers at startup
- Compile-time diagnostics for missing handlers (DSOFT001), duplicate handler registrations (DSOFT002, DSOFT003), and mixed registration API (DSOFT007)
- `MediatorBuilder` fluent API — single-call `AddMediator(configure)` with `AddOpenBehavior`, `AddRequestPreProcessor`, `AddParallelNotificationPublisher`, and more
- Strong naming — all assemblies signed with `PublicKeyToken=6c7e753832e8eb05` for enterprise compatibility

## See Also

- [Registration Order](registration-order.md) — understand when to call `Precompile*` methods
- [CQRS (Commands & Queries)](../concepts/cqrs.md) — use `ICommand<T>` and `IQuery<T>` for semantic clarity
- [Benchmarks](../benchmarks.md) — see how DSoftStudio.Mediator compares to MediatR, Mediator SG, and DispatchR
- [Migration from MediatR](migration-from-mediatr.md) — coming from MediatR? Start here
