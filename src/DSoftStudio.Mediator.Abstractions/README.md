![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

# DSoftStudio.Mediator.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.Abstractions.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.Abstractions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Contracts (interfaces and base types) for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). Reference this package in **domain**, **application-core**, and **test** projects that need `ISender`, `IPublisher`, `IMediator`, `IRequest`, `INotification`, and related abstractions — without pulling in the runtime or source generators.

## Installation

```shell
dotnet add package DSoftStudio.Mediator.Abstractions
```

## When to Use This Package

| Project type | Package to reference | Why |
|---|---|---|
| **Host / Composition root** | `DSoftStudio.Mediator` | Full runtime + DI registration + source generators |
| **Domain / Application layer** | `DSoftStudio.Mediator.Abstractions` ← this one | Interfaces only — no generators, no generated code |
| **Unit test project** | `DSoftStudio.Mediator.Abstractions` ← this one | Fully mock-safe — no interceptors generated |

This is the **recommended architecture** for testability. See the [cross-project-mocking sample](https://github.com/DSoftStudio/Mediator/tree/main/samples/cross-project-mocking) for a complete working example.

## Included Interfaces

### Dispatchers

| Interface | Description |
|---|---|
| `ISender` | Sends requests (commands/queries) through the pipeline |
| `IPublisher` | Publishes notifications to all registered handlers |
| `IMediator` | Combines `ISender` + `IPublisher` + stream dispatch |

### Requests & Handlers

| Interface | Description |
|---|---|
| `IRequest<TResponse>` | Marker for a request that returns `TResponse` |
| `IRequestHandler<TRequest, TResponse>` | Handles a request and returns a response |
| `ICommand<TResponse>` | Semantic alias for `IRequest<TResponse>` — write operations (CQRS) |
| `ICommand` | Non-generic marker for runtime command detection |
| `ICommandHandler<TCommand, TResponse>` | Semantic alias for `IRequestHandler` — command handlers |
| `ICommandHandler<TCommand>` | Convenience alias for commands returning `Unit` |
| `IQuery<TResponse>` | Semantic alias for `IRequest<TResponse>` — read operations (CQRS) |
| `IQuery` | Non-generic marker for runtime query detection |
| `IQueryHandler<TQuery, TResponse>` | Semantic alias for `IRequestHandler` — query handlers |

### Notifications

| Interface | Description |
|---|---|
| `INotification` | Marker for a notification (domain event) |
| `INotificationHandler<TNotification>` | Handles a notification — multiple handlers per type |
| `INotificationPublisher` | Strategy for dispatching notifications (sequential, parallel, custom) |

### Streams

| Interface | Description |
|---|---|
| `IStreamRequest<TResponse>` | Marker for a streaming request returning `IAsyncEnumerable<TResponse>` |
| `IStreamRequestHandler<TRequest, TResponse>` | Handles a stream request |
| `IStreamPipelineBehavior<TRequest, TResponse>` | Pipeline behavior for stream requests |

### Pipeline

| Interface | Description |
|---|---|
| `IPipelineBehavior<TRequest, TResponse>` | Wraps request execution — logging, validation, transactions, etc. |
| `IRequestPreProcessor<TRequest>` | Runs before the handler — simpler than a full behavior |
| `IRequestPostProcessor<TRequest, TResponse>` | Runs after the handler — audit logging, caching, etc. |
| `IRequestExceptionHandler<TRequest, TResponse>` | Handles exceptions — can suppress and provide fallback responses |

### Types

| Type | Description |
|---|---|
| `Unit` | Void return type for requests with no meaningful result |
| `RequestExceptionHandlerState<TResponse>` | Mutable state for exception handlers to signal handling |
| `MediatorHandlerRegistrationAttribute` | Assembly-level attribute for cross-project handler discovery |

## Usage Examples

### Define a command and handler

```csharp
using DSoftStudio.Mediator.Abstractions;

// Command (write operation)
public sealed record CreateOrderCommand(string ProductName, int Quantity) : ICommand<int>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, int>
{
    public ValueTask<int> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // ... create order logic
        return new(orderId);
    }
}
```

### Define a query and handler

```csharp
// Query (read operation)
public sealed record GetOrderQuery(int OrderId) : IQuery<string>;

public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
{
    public ValueTask<string> Handle(GetOrderQuery request, CancellationToken ct)
    {
        return new($"Order #{request.OrderId}");
    }
}
```

### Define a notification and handler

```csharp
public sealed record OrderPlaced(int OrderId) : INotification;

public sealed class SendOrderEmail : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken ct)
    {
        // ... send email
        return Task.CompletedTask;
    }
}
```

### Define a pipeline behavior

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next.Handle(request, ct);
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

### Mock ISender in tests

```csharp
// Always use the explicit two-generic-parameter form:
var sender = new Mock<ISender>();
sender.Setup(s => s.Send<CreateOrderCommand, int>(
        It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(1001);
```

## Target Framework

This package targets **.NET Standard 2.0** — compatible with .NET 8+, .NET Framework 4.6.1+, and any runtime that supports .NET Standard 2.0.

## Related Packages

| Package | Description |
|---|---|
| [`DSoftStudio.Mediator`](https://www.nuget.org/packages/DSoftStudio.Mediator) | Runtime + DI + source generators |
| [`DSoftStudio.Mediator.OpenTelemetry`](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry) | Distributed tracing + metrics |
| [`DSoftStudio.Mediator.FluentValidation`](https://www.nuget.org/packages/DSoftStudio.Mediator.FluentValidation) | Automatic request validation |
| [`DSoftStudio.Mediator.HybridCache`](https://www.nuget.org/packages/DSoftStudio.Mediator.HybridCache) | Multi-layer caching |

## Documentation

Full documentation: [docs.dsoftstudio.com/mediator](https://docs.dsoftstudio.com/mediator)

## License

[MIT](https://opensource.org/licenses/MIT)
