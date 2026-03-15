---
layout: default
title: "Migration from MediatR - DSoftStudio.Mediator"
description: "Step-by-step guide to migrate from MediatR to DSoftStudio.Mediator."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Migrating from MediatR

This guide targets **MediatR 12–14.x** (the current `MediatR` unified package). Earlier versions that used separate `MediatR.Extensions.Microsoft.DependencyInjection` follow the same steps.

## 1. Replace the NuGet package

```shell
dotnet remove package MediatR
dotnet add package DSoftStudio.Mediator
```

## 2. Update namespaces

```diff
- using MediatR;
+ using DSoftStudio.Mediator.Abstractions;
+ using DSoftStudio.Mediator;
```

## 3. Update service registration

```diff
- services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
+ services
+     .AddMediator()
+     .RegisterMediatorHandlers()
+     .PrecompilePipelines()
+     .PrecompileNotifications()
+     .PrecompileStreams();
```

## 4. Update handler return types

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

## 5. Update pipeline behavior signatures

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

## 6. Update Send calls (usually no change needed)

The source generator creates typed extension methods, so `mediator.Send(new Ping())` works out of the box with full type inference — zero reflection, best performance:

```csharp
// Works automatically via generated extension method (recommended)
var result = await mediator.Send(new Ping());

// Explicit generics also work
var result = await mediator.Send<Ping, int>(new Ping());
```

## 7. Update Pre/Post processor return types

Pre/post processors use `ValueTask` instead of MediatR's `Task`:

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

## 8. Handler lifetimes (behavioral difference)

MediatR registers all handlers as **Transient** by default. DSoftStudio.Mediator uses **automatic lifetime detection**:

- **Stateless handlers** (no constructor parameters) → **Singleton** (zero allocation per call)
- **Handlers with DI dependencies** → **Transient** (safe default)

You can override any handler's lifetime after `RegisterMediatorHandlers()` — the last registration wins. Place overrides **before** `PrecompilePipelines()` so the pipeline chain picks up the correct lifetime:

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

## What stays the same

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Request marker | `IRequest<TResponse>` | `IRequest<TResponse>` |
| Notification marker | `INotification` | `INotification` |
| Notification handler | `Task Handle(T, CancellationToken)` | `Task Handle(T, CancellationToken)` |
| Stream request | `IStreamRequest<TResponse>` | `IStreamRequest<TResponse>` |
| Send syntax | `mediator.Send(new Ping())` | `mediator.Send(new Ping())` (via generated extension) |
| Open generic registration | `services.AddTransient(typeof(IPipelineBehavior<,>), ...)` | Same |

## What changes

| Concept | MediatR | DSoftStudio.Mediator |
|---|---|---|
| Handler return type | `Task<TResponse>` | `ValueTask<TResponse>` |
| Behavior `next` param | `RequestHandlerDelegate<TResponse>` | `IRequestHandler<TRequest, TResponse>` |
| Calling next | `await next()` | `await next.Handle(request, ct)` |
| Pre/Post processor return | `Task` | `ValueTask` |
| Handler lifetime (default) | All Transient | Stateless → Singleton, with DI deps → Transient |
| Namespace | `using MediatR;` | `using DSoftStudio.Mediator.Abstractions;` |

## 9. Optional: Consolidate with self-handling requests

For simple commands/queries where the handler is trivial, you can eliminate the handler class entirely by placing a `static Execute` method inside the request. This is particularly useful in large MediatR migrations with hundreds of small command/query pairs:

```diff
- public record Ping(int Value) : IRequest<int>;
-
- public class PingHandler : IRequestHandler<Ping, int>
- {
-     public ValueTask<int> Handle(Ping request, CancellationToken ct)
-         => new ValueTask<int>(request.Value * 2);
- }
+ public record Ping(int Value) : ICommand<int>
+ {
+     internal static int Execute(Ping cmd) => cmd.Value * 2;
+ }
```

See [Self-Handling Requests](../features/self-handling-requests.md) for full documentation.
