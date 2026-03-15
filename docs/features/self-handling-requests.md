# Self-Handling Requests

For simple request types where a separate handler class adds ceremony without value, you can place a `static Execute` method directly inside the request class (or record). The source generator discovers this pattern at compile time and emits an internal adapter that bridges it to `IRequestHandler<TRequest, TResponse>` — no manual wiring required.

This is especially useful when **migrating from MediatR** codebases that have many small command/query classes, or when you prefer a more concise single-file style.

## Basic Example

```csharp
public record Ping(int Value) : ICommand<int>
{
    internal static int Execute(Ping cmd)
        => cmd.Value * 2;
}

// Send as usual — the generated adapter handles dispatch:
var result = await mediator.Send(new Ping(21)); // 42
```

## With Dependency Injection

Service parameters in the `Execute` signature are resolved from DI automatically:

```csharp
public record Greet(string Name) : IQuery<string>
{
    internal static string Execute(Greet query, IGreetingService greeter)
        => greeter.Greet(query.Name);
}
```

Register the service as you normally would — the self-handler adapter receives it via constructor injection:

```csharp
services.AddSingleton<IGreetingService, GreetingService>();
services.AddMediator().RegisterMediatorHandlers().PrecompilePipelines();
```

## Async Handlers

`Task<T>`, `ValueTask<T>`, `void`, and `Task` return types are all supported:

```csharp
// Async Task<T>
public record FetchData(int Id) : IQuery<DataDto>
{
    internal static async Task<DataDto> Execute(FetchData query, IDataStore store, CancellationToken ct)
        => await store.GetByIdAsync(query.Id, ct);
}

// Void (ICommand<Unit>)
public record FireAndForget(string Message) : ICommand<Unit>
{
    internal static void Execute(FireAndForget cmd, ILogger logger)
        => logger.LogInformation(cmd.Message);
}
```

## How It Works

The source generator:

1. Discovers classes/records implementing `IRequest<T>` (or `ICommand<T>` / `IQuery<T>`) that contain a `static Execute` method
2. Emits an internal `__SelfHandler_*` adapter class implementing `IRequestHandler<TRequest, TResponse>`
3. Registers the adapter in DI — **Singleton** if stateless (no DI parameters), **Transient** if it has service dependencies
4. Integrates with all existing infrastructure: pipeline behaviors, pre/post processors, exception handlers, typed `Send()` extensions, and `ValidateMediatorHandlers()` validation

## Execute Method Signature

The `Execute` method accepts parameters in any order. The generator identifies each parameter by type:

| Parameter type | Resolution |
|---|---|
| The request type itself (`TRequest`) | Passed from `Handle(request, ct)` |
| `CancellationToken` | Forwarded from `Handle(request, ct)` |
| Any other type | Resolved from DI via constructor injection |

## Supported Return Types

| Return type | Response type | Behavior |
|---|---|---|
| `T` | `TResponse` | Sync — wrapped in `ValueTask<T>` |
| `Task<T>` | `TResponse` | Async — wrapped in `ValueTask<T>` |
| `ValueTask<T>` | `TResponse` | Async — returned directly |
| `void` | `Unit` | Sync void — returns `Unit.Value` |
| `Task` | `Unit` | Async void — awaits, returns `Unit.Value` |

> **Note:** Self-handling requests participate in the same dispatch path as normal handlers — `HandlerCache` ThreadStatic caching, pipeline behaviors, and compile-time typed extensions all apply. The static `Execute` call is a direct call (no virtual dispatch), making it marginally faster than interface dispatch inside the adapter.
