---
layout: default
title: "Handler Validation - DSoftStudio.Mediator"
description: "Fail-fast compile-time validation for request handlers."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Handler Validation

## Overview

`ValidateMediatorHandlers()` is a source-generated extension method on `IServiceProvider` that detects misconfigured handlers **at startup** — before the first request is processed. It resolves every registered handler, pipeline chain, and notification handler from DI, and throws an `AggregateException` containing everything it found.

It reports two kinds of problem: a registration that cannot be resolved at all, and a registration that resolves perfectly well but will not behave the way it was written. The second kind is the one nothing else catches — see [Registration-order problems](#registration-order-problems).

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediator(b =>
{
    b.AddOpenBehavior(typeof(LoggingBehavior<,>));
    b.AddRequestPreProcessor<ValidationPreProcessor>();
});

var app = builder.Build();

// Fail fast — detect missing handlers, broken dependencies, incomplete pipelines
app.Services.ValidateMediatorHandlers();

app.Run();
```

This works with both registration approaches — `AddMediator(configure)` and the step-by-step `RegisterMediatorHandlers()` + `PrecompilePipelines()`. The validation method operates on the built `IServiceProvider`, not on the registration API.

## What It Validates

| Handler type | Validation |
|---|---|
| Request handlers | `GetRequiredService<IRequestHandler<T,R>>()` + `GetService<PipelineChainHandler<T,R>>()` |
| Self-handling requests | `GetRequiredService<IRequestHandler<T,R>>()` (validates the generated adapter and its DI dependencies) |
| Notification handlers | `GetServices<INotificationHandler<T>>()` + materialization of all implementations |
| Stream handlers | `GetRequiredService<IStreamRequestHandler<T,R>>()` + `GetService<StreamPipelineChainHandler<T,R>>()` |
| Late-registered components | `GetServices<LatePipelineComponentReport>()` — names every pipeline component a second `AddMediator(configure)` registered after the chains were frozen |

## Registration-order problems

These resolve without error and would run misconfigured. `DSOFT010` catches them at compile time when
the registration and the scan sit in the same method; everything else — another method, another
assembly, another module — reaches you here.

| Reported | Why it matters |
|---|---|
| A `Transient` pipeline behavior whose chain is not Transient | The chain is constructed once and holds that behavior, so it is shared for the life of the chain instead of built per dispatch — the opposite of what `Transient` asked for. |
| An `IMediatorDispatchObserver` with no chain anywhere | The observer runs inside the pipeline chain, so with no chain built it never runs at all. |
| Components registered by a second `AddMediator(configure)` | The overload runs your lambda on every call, but the scan behind it returns early once it has run — so those components get no chain rebuilt around them. They either never run, or a Singleton chain from the first scan captures them and the container refuses to build under `ValidateScopes`. |

The fix is the same in every case: register the whole pipeline before the scan, in the first
`AddMediator(configure)`.

## When Validation Fails

```
System.AggregateException: One or more mediator handlers failed validation.
---> System.InvalidOperationException: No service for type 'IRequestHandler<CreateUser, Guid>'
---> System.InvalidOperationException: Unable to resolve service for type 'IUserRepository'
     while attempting to activate 'GetUserHandler'
```

> **Recommendation:** Call `ValidateMediatorHandlers()` in development and staging environments. In production, consider gating it behind a configuration flag to avoid the startup cost of resolving every handler.

## How It Works

The source generator emits `ValidateMediatorHandlers()` at compile time with explicit `GetRequiredService<T>()` calls for every handler it discovered. This means:

- **No reflection** — no assembly scanning or type walking at runtime
- **No false positives** — it validates exactly the same handlers the mediator will dispatch to
- **All failures reported at once** — the method collects all resolution errors into a single `AggregateException`, so you see every problem in one pass

## See Also

- [Quick Start](../getting-started/quick-start.md) — get started in 5 minutes
- [Registration Order](../getting-started/registration-order.md) — `AddMediator(configure)` vs step-by-step registration
- [Production Validation](../architecture/production-validation.md) — full test catalog (failure injection, chaos, concurrency)
