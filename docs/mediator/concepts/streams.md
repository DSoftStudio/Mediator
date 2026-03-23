---
layout: default
title: "Streams - DSoftStudio.Mediator"
description: "Stream responses with IAsyncEnumerable."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Streams

Async streaming returns an `IAsyncEnumerable<T>` from a handler. This is useful for large datasets, event feeds, and progressive responses where buffering the entire result set in memory is impractical.

```csharp
public record StreamNumbers() : IStreamRequest<int>;

public class StreamNumbersHandler
    : IStreamRequestHandler<StreamNumbers, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamNumbers request,
        CancellationToken ct)
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
```

Consume the stream:

```csharp
await foreach (var n in mediator.CreateStream(new StreamNumbers()))
{
    Console.WriteLine(n);
}
```

Stream handlers support cancellation through the `CancellationToken` parameter and can be combined with `IStreamPipelineBehavior<TRequest, TResponse>` for cross-cutting concerns like logging or rate limiting.

## Stream Pipeline Behaviors

Stream behaviors wrap the entire `IAsyncEnumerable<T>` flow — they can intercept before the first item is yielded and after the last:

```csharp
public class StreamLoggingBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Console.WriteLine($"Stream started: {typeof(TRequest).Name}");
        int count = 0;

        await foreach (var item in next.Handle(request, ct))
        {
            count++;
            yield return item;
        }

        Console.WriteLine($"Stream completed: {count} items");
    }
}
```

Register as an open generic:

```csharp
services.AddTransient(typeof(IStreamPipelineBehavior<,>), typeof(StreamLoggingBehavior<,>));
```

## Using Streams in ASP.NET Minimal APIs

Streams integrate naturally with ASP.NET Core's `IAsyncEnumerable<T>` support for Server-Sent Events (SSE) and chunked responses:

```csharp
app.MapGet("/numbers", (IMediator mediator) =>
    mediator.CreateStream(new StreamNumbers()));
```

## Performance

Stream dispatch uses the same source-generated pattern as `Send()` — the `StreamGenerator` emits static factory delegates for each stream type:

| Metric | DSoft | MediatR |
|---|---|---|
| `CreateStream()` latency | 45.5 ns | 122.9 ns |
| Allocation | 232 B | 624 B |

The 232 B allocation is the `IAsyncEnumerator<T>` state machine — this is inherent to `IAsyncEnumerable<T>` and cannot be eliminated. DSoftStudio.Mediator adds zero overhead beyond the state machine itself.

## See Also

- [Requests & Handlers](requests-and-handlers.md) — the core request/response building blocks
- [Source Generators](../architecture/source-generators.md) — how `StreamGenerator` emits stream dispatch
- [Registration Order](../getting-started/registration-order.md) — call `PrecompileStreams()` after handler registration
- [Benchmarks](../benchmarks.md) — stream latency comparison across 4 libraries
