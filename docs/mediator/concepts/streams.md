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
services.AddScoped(typeof(IStreamPipelineBehavior<,>), typeof(StreamLoggingBehavior<,>));
```

> **Register stream behaviors before `PrecompileStreams()`.** That call decides whether a behavior
> chain is built for each stream pair. If a pair has no behavior registered by then, the handler
> streams unwrapped and behaviors added afterwards never run. Nothing throws, but it is no longer
> unreported: **DSOFT010** flags a stream behavior registered after `PrecompileStreams()` on the same
> collection in the same method — the fluent `services.AddMediator().PrecompileStreams()` form included
> — and `ValidateMediatorHandlers()` reports what the analyzer cannot see. Only `PrecompileStreams()`
> counts, or `AddMediator(configure)`, which precompiles everything: a stream behavior registered after
> `PrecompileNotifications()` but before `PrecompileStreams()` is correctly ordered.

Unlike the request pipeline, the stream pipeline composes behaviors only: there are no stream
pre-processors, post-processors or exception handlers.

### What is deferred, and what is not

Writing the handler or a behavior as an iterator defers *its body* until the caller starts
enumerating — that is the C# iterator rule, not something the mediator adds. Dispatch itself is
eager: `CreateStream` resolves the handler and builds the behavior chain when it is called. A stream
created inside a scope has therefore already captured both, and enumerating it after the scope is
disposed uses what it captured.

An iterator behavior returns its own stream, so a token the consumer supplies with
`WithCancellation` stops there. Annotate the token parameter with `[EnumeratorCancellation]` and
forward the token when enumerating `next`, as the example above does.

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
| `CreateStream()` latency | 30.7 ns | 112.8 ns |
| Allocation | 88 B | 464 B |

Measured on .NET 11. On .NET 10: 44.8 ns / 232 B against 126.3 ns / 624 B — runtime async removed part
of the enumerator's cost for everyone.

The 88 B allocation is the `IAsyncEnumerator<T>` state machine — this is inherent to `IAsyncEnumerable<T>` and cannot be eliminated. DSoftStudio.Mediator adds zero overhead beyond the state machine itself.

## See Also

- [Requests & Handlers](requests-and-handlers.md) — the core request/response building blocks
- [Source Generators](../architecture/source-generators.md) — how `StreamGenerator` emits stream dispatch
- [Registration Order](../getting-started/registration-order.md) — call `PrecompileStreams()` after handler registration
- [Benchmarks](../benchmarks.md) — stream latency comparison across 4 libraries
