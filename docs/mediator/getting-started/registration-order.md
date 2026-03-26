---
layout: default
title: "Registration Order - DSoftStudio.Mediator"
description: "Understand the correct DI registration order for handlers and pipelines."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Registration Order

## Recommended: `AddMediator(configure)` (v1.2.0+)

The simplest way to register the mediator is the single-call `AddMediator(configure)` overload. It handles the entire registration sequence automatically:

```csharp
services.AddMediator(builder =>
{
    builder.AddOpenBehavior(typeof(LoggingBehavior<,>));
    builder.AddRequestPreProcessor<ValidationPreProcessor>();
    builder.AddRequestPostProcessor<AuditPostProcessor>();
    builder.AddRequestExceptionHandler<GlobalExceptionHandler>();
    builder.AddParallelNotificationPublisher();
});
```

Internally, this single call executes in order:
1. `AddMediator()` — registers core services (`IMediator`, `ISender`, `IPublisher`)
2. `RegisterMediatorHandlers()` — discovers and registers all handlers
3. `configure(builder)` — your pipeline customization
4. `PrecompilePipelines()` + `Freeze()` — precompiles dispatch tables

> **Do not mix `AddMediator(configure)` with individual registration calls.**
> Calling `RegisterMediatorHandlers()` or `PrecompilePipelines()` separately alongside
> `AddMediator(configure)` causes redundant registrations. The compile-time diagnostic
> **DSOFT007** will warn you if mixed usage is detected. Runtime idempotency guards prevent
> actual double-registration, but the intent should be clear in your code.

## Alternative: Step-by-step Registration

If you need fine-grained control over the registration order, use the individual methods.
The `Precompile*` methods inspect the `IServiceCollection` at startup to determine dispatch strategies and chain lifetimes. **All service registrations must happen before the corresponding `Precompile*` call.**

```csharp
services
    .AddMediator()                // 1. Core mediator services
    .RegisterMediatorHandlers();  // 2. Source-generated handler registrations

// 3. Register behaviors, processors, exception handlers
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IRequestPreProcessor<>), typeof(ValidationPreProcessor<>));
services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(AuditPostProcessor<,>));

// 4. Override handler lifetimes (optional)
services.AddScoped<IRequestHandler<MyRequest, MyResponse>, MyHandler>();

// 5. Register notification strategies (optional)
services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();

// 6. Precompile — inspects all registrations above
services
    .PrecompilePipelines()        // scans for IPipelineBehavior, Pre/Post processors, exception handlers
    .PrecompileNotifications()    // builds static dispatch arrays for each INotification type
    .PrecompileStreams();         // builds static factory delegates for each IStreamRequest type
```

## What Each Method Inspects

| Method | What it inspects | What to register before |
|---|---|---|
| `PrecompilePipelines()` | `IPipelineBehavior<,>`, `IRequestPreProcessor<>`, `IRequestPostProcessor<,>`, `IRequestExceptionHandler<,>`, handler lifetimes | Behaviors, processors, exception handlers, handler overrides |
| `PrecompileNotifications()` | `INotificationHandler<>` | Notification handler overrides |
| `PrecompileStreams()` | `IStreamRequestHandler<,>` | Stream handler overrides |

## Pipeline Chain Lifetimes

`PrecompilePipelines()` determines each `PipelineChainHandler` lifetime based on the registered components: **Singleton** when all components are Singleton, **Scoped** when any is Scoped, **Transient** when any is Transient. Registrations added after the `Precompile*` calls will not be picked up by the dispatch tables.
