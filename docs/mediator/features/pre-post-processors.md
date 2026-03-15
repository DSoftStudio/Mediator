---
layout: default
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Pre/Post Processors

For cross-cutting concerns that only need a "before" or "after" hook, pre/post processors are simpler than full pipeline behaviors — no `next` parameter, no chain responsibility.

```
PreProcessor₁ → PreProcessor₂ → Handler → PostProcessor₁ → PostProcessor₂
```

## Pre-Processors

Run before the handler. If a pre-processor throws, the handler is not invoked.

```csharp
public class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken ct)
    {
        // Validate before the handler runs — throw to short-circuit
        if (request is ICommand command)
            Console.WriteLine($"Validating {typeof(TRequest).Name}");

        return ValueTask.CompletedTask;
    }
}
```

## Post-Processors

Run after the handler completes successfully. Not invoked if the handler throws.

```csharp
public class AuditPostProcessor<TRequest, TResponse>
    : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        Console.WriteLine($"{typeof(TRequest).Name} → {response}");
        return ValueTask.CompletedTask;
    }
}
```

## Registration

Register as open generics:

```csharp
services.AddTransient(typeof(IRequestPreProcessor<>), typeof(ValidationPreProcessor<>));
services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(AuditPostProcessor<,>));
```
