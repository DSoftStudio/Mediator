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

Call `ValidateMediatorHandlers()` after building the service provider to detect misconfigured handlers at startup — before the first request is processed. The method resolves every registered handler, pipeline chain, and notification handler from DI, and throws an `AggregateException` containing all failures if any handler cannot be resolved.

```csharp
var app = builder.Build();

// Fail fast — detect missing handlers, broken dependencies, incomplete pipelines
app.Services.ValidateMediatorHandlers();

app.Run();
```

This is a source-generated method — it validates exactly the handlers discovered at compile time. No reflection, no assembly scanning.

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
