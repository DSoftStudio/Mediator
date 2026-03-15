---
layout: default
title: "Pipeline Behaviors - DSoftStudio.Mediator"
description: "Zero-allocation pipeline behaviors via interface dispatch."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Pipeline Behaviors

Pipeline behaviors wrap handler execution, forming a chain:

```
Behavior1 → Behavior2 → … → Handler
```

Each behavior can run logic before and after the next step in the pipeline. The `next` parameter is an `IRequestHandler` — calling `next.Handle(request, ct)` advances the chain via interface dispatch (virtual call) instead of a delegate invocation, enabling **zero-allocation behavior pipelines**.

## Pipeline Allocation

Allocations stay flat at 72 B regardless of pipeline depth:

```mermaid
xychart-beta
    title "Allocation vs Pipeline Depth"
    x-axis "Behaviors" [0, 1, 3, 5]
    y-axis "Bytes" 0 --> 100
    line [72, 72, 72, 72]
```

| Behaviors | DSoft (alloc) | MediatR (alloc) |
|-----------|---------------|-----------------|
| 0         | 72 B          | 272 B           |
| 1         | 72 B          | 544 B           |
| 3         | 72 B          | 800 B           |
| 5         | 72 B          | 1,088 B         |

## Example

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        return await next.Handle(request, ct);
    }
}
```

Register behaviors as open generics:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

Behaviors execute in registration order. The first registered behavior is the outermost wrapper.
