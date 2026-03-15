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
