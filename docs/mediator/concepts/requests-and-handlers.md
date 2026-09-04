---
layout: default
title: "Requests and Handlers - DSoftStudio.Mediator"
description: "Define requests and handlers with compile-time dispatch."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Requests & Handlers

Requests and handlers are the core building blocks of the mediator pattern. A **request** is a message object that carries input data, and a **handler** is the component that processes it and returns a response.

## Defining a Request

A request implements `IRequest<TResponse>` where `TResponse` is the type returned by the handler:

```csharp
public record Ping() : IRequest<int>;

public record GetUser(Guid Id) : IRequest<UserDto>;

public record CreateOrder(OrderInput Input) : IRequest<Guid>;
```

## Defining a Handler

A handler implements `IRequestHandler<TRequest, TResponse>` and contains the business logic:

```csharp
public class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken ct)
        => new ValueTask<int>(42);
}
```

Handlers return `ValueTask<TResponse>` instead of `Task<TResponse>`. This avoids heap allocation when the result is available synchronously — a key design choice for zero-allocation dispatch.

## Sending a Request

Inject `IMediator` and call `Send`:

```csharp
var result = await mediator.Send(new Ping());
```

The mediator resolves the correct handler at compile time via source-generated dispatch tables — no reflection, no `MakeGenericType`, no dictionary lookups in the hot path.

## Handler Lifetime

DSoftStudio.Mediator automatically optimizes handler lifetimes:

| Handler Type | Lifetime | Why |
|---|---|---|
| **Stateless** (no constructor parameters) | **Singleton** | Zero per-call allocation — the handler is instantiated once and reused |
| **All dependencies Singleton** | **Singleton** | Nothing in the graph is shorter-lived, so the handler is safe to share. An open-generic registration counts: `ILogger<T>` and `IOptions<T>` resolve against their open registration exactly as the container does |
| **Any dependency Scoped** | **Scoped** | Cached per scope, and the handler is only ever resolved inside one |
| **Any dependency Transient or unregistered** | **Transient** | Not safe to capture for longer — and a Transient handler takes its whole pipeline chain down with it, so the chain is rebuilt per dispatch |

This is different from MediatR, where all handlers are registered as Transient by default — and the optimization is not confined to stateless handlers. The decision is deferred until every registration is present, and a closed dependency is matched against its open-generic registration the way the container matches it, so a handler injecting an `ILogger<T>` is promoted to Singleton and one injecting a scoped `DbContext` is promoted to Scoped. Only a transient or genuinely unregistered dependency leaves a handler Transient.

You can override the default lifetime by registering the handler manually before calling `RegisterMediatorHandlers()`:

```csharp
services.AddScoped<IRequestHandler<GetUser, UserDto>, GetUserHandler>();
services.RegisterMediatorHandlers(); // will not override existing registrations
```

## Void Requests

For requests that don't return a meaningful value, use `Unit`:

```csharp
public record DeleteUser(Guid Id) : IRequest<Unit>;

public class DeleteUserHandler : IRequestHandler<DeleteUser, Unit>
{
    public ValueTask<Unit> Handle(DeleteUser request, CancellationToken ct)
    {
        // delete logic
        return new ValueTask<Unit>(Unit.Value);
    }
}
```

## Next Steps

- [CQRS](cqrs.md) — semantic `ICommand<T>` / `IQuery<T>` wrappers
- [Self-Handling Requests](../features/self-handling-requests.md) — embed handler logic inside the request itself
- [Pipeline Behaviors](../features/pipeline-behaviors.md) — add cross-cutting concerns around handler execution
