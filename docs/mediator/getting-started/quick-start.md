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

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

> **Registration order matters.** The `Precompile*` methods inspect the service collection to determine dispatch strategies and lifetimes. Register all behaviors, processors, exception handlers, notification strategies, and handler lifetime overrides **before** calling `PrecompilePipelines()` / `PrecompileNotifications()` / `PrecompileStreams()`. See [Registration Order](registration-order.md) for details.

## 3. Send a Request

```csharp
var result = await mediator.Send(new Ping());
```

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
- Compile-time diagnostics for missing handlers (DSOFT001) and duplicate handler registrations (DSOFT002, DSOFT003)
