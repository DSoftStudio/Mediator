# Registration Order

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
