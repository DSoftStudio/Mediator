---
layout: default
title: "Installation - DSoftStudio.Mediator"
description: "Install DSoftStudio.Mediator and companion NuGet packages for .NET 8+ projects."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Installation

## Core Package

```shell
dotnet add package DSoftStudio.Mediator
```

## Companion Packages

```shell
dotnet add package DSoftStudio.Mediator.OpenTelemetry      # Distributed tracing + metrics
dotnet add package DSoftStudio.Mediator.FluentValidation    # Request validation
dotnet add package DSoftStudio.Mediator.HybridCache         # Query caching
```

## Requirements

- .NET 8.0 or later
- Source generators require the Roslyn compiler (included with .NET SDK)

## Strong Naming

All assemblies are signed with `PublicKeyToken=6c7e753832e8eb05` (v1.2.0+). This enables:
- Installation in the GAC
- Referencing from other strong-named assemblies
- Tamper detection

## Cross-Project Handler Discovery

`AddMediator(configure)` (and the lower-level `RegisterMediatorHandlers()`) automatically
discovers all handler implementations (`IRequestHandler<,>`, `INotificationHandler<>`,
`IStreamRequestHandler<,>`, and their CQRS aliases `ICommandHandler<,>`, `IQueryHandler<,>`)
across **all referenced projects** — no manual registration required.

Each project that references `DSoftStudio.Mediator` emits
`[assembly: MediatorHandlerRegistration]` attributes at compile time. Downstream projects
read these attributes to build the complete handler registry, without runtime reflection
or assembly scanning.

This works seamlessly with **Clean Architecture** setups:

```
Host/API  →  references Application & Infrastructure
              └─ AddMediator(configure) discovers handlers from both
```

> **Tip:** every project that defines handlers should reference the
> `DSoftStudio.Mediator` NuGet package so the source generator can run and emit
> the assembly attributes (Phase 1 — fast path). However, projects that only
> reference `DSoftStudio.Mediator.Abstractions` are still discovered automatically
> via type-based scanning (Phase 2 — fallback). The host project that calls
> `AddMediator(configure)` must always reference `DSoftStudio.Mediator`.

## Required Namespaces

The mediator uses two namespaces — which ones you need depends on the project layer:

| Project layer | Namespace | Purpose |
|---|---|---|
| Domain / Application | `DSoftStudio.Mediator.Abstractions` | `ISender`, `IMediator`, `IRequest<T>`, `ICommand<T>`, `INotification`, handler interfaces |
| Host / API | `DSoftStudio.Mediator` | `AddMediator()` DI extensions, typed `Send()` / `CreateStream()` extension methods |

In your **host/startup** project (where you call `AddMediator(configure)` and inject `ISender`), you typically need both:

```csharp
using DSoftStudio.Mediator.Abstractions; // ISender, IMediator, ICommand<T>, IRequest<T>, etc.
using DSoftStudio.Mediator;              // Typed Send() / CreateStream() extensions + AddMediator() DI extensions
```

In **domain or application** layer projects (where you define requests, handlers, and inject `ISender`), only the abstractions namespace is required:

```csharp
using DSoftStudio.Mediator.Abstractions;
```

> **Why the second namespace matters in the host project:** the source generator emits typed
> extension methods (e.g. `Send(MyCommand)`) in the `DSoftStudio.Mediator` namespace.
> Without this `using`, the compiler falls back to the generic
> `ISender.Send<TRequest, TResponse>()` and reports **CS0411** because it cannot infer
> both type arguments.

**Tip:** add the namespaces you need to a `GlobalUsings.cs` file so every file in the project picks them up automatically:

```csharp
// GlobalUsings.cs
global using DSoftStudio.Mediator;
global using DSoftStudio.Mediator.Abstractions;
```

## Native AOT and Trimming

Both packages ship with `IsAotCompatible` and `IsTrimmable` enabled. The hot execution path uses no reflection, no `MakeGenericType`, no `Expression.Compile`, and no dynamic method generation — all handler discovery and dispatch wiring are performed at compile time by Roslyn source generators.

This makes the mediator suitable for:

- **Native AOT ASP.NET applications** — publish self-contained, ahead-of-time compiled APIs
- **Serverless / cloud functions** — fast cold start with minimal memory footprint
- **Containerized microservices** — smaller images, no JIT warm-up
- **High-density cloud workloads** — reduced memory per instance

The `Publish(object)` and `Send(object)` overloads (runtime-typed dispatch) are also AOT-safe — they use compile-time generated `FrozenDictionary<Type, DispatchDelegate>` dispatch tables populated by the source generator, with no `MakeGenericType` at runtime.

## See Also

- [Quick Start](quick-start.md) — get started in 5 minutes
- [Migration from MediatR](migration-from-mediatr.md) — coming from MediatR? Step-by-step guide
- [Registration Order](registration-order.md) — `AddMediator(configure)` vs step-by-step registration