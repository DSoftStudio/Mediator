---
layout: default
title: "CQRS - DSoftStudio.Mediator"
description: "Implement CQRS with compile-time typed commands and queries."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# CQRS Support (Commands and Queries)

While the mediator can be used directly with `IRequest<TResponse>`, many applications prefer a CQRS-style separation between commands (writes) and queries (reads). `ICommand<TResponse>` and `IQuery<TResponse>` provide semantic clarity while still flowing through the same mediator pipeline.

- `ICommand<TResponse>` represents operations that **modify application state** (writes).
- `IQuery<TResponse>` represents **read-only** operations.
- Both inherit from `IRequest<TResponse>` and flow through the same pipeline.

CQRS markers enable pipeline behaviors to target commands and queries differently — for example, wrapping commands in transactions while applying caching only to queries.

## Commands

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

## Queries

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

## Targeting Behaviors by Message Type

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
