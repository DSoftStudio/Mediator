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

`ValidateMediatorHandlers()` is a source-generated extension method on `IServiceProvider` that detects misconfigured handlers **at startup** — before the first request is processed. It resolves every registered handler, pipeline chain, and notification handler from DI, and throws an `AggregateException` containing all failures if any handler cannot be resolved.

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
