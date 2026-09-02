---
layout: default
title: "Pre/Post Processors - DSoftStudio.Mediator"
description: "Run logic before and after request handling."
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
[ exception handlers: PreProcessor₁ → PreProcessor₂ → Behavior₁ → … → Handler ] → PostProcessor₁ → PostProcessor₂
```

Note where the exception handlers sit: they wrap the pre-processors, the behavior chain and the
handler — but **not** the post-processors. By the time a post-processor runs a response already
exists, so substituting a different one would leave the post-processors that already ran having
observed a response that is not the one returned.

## Pre-Processors

Run before the handler. If a pre-processor throws, neither the behaviors nor the handler are
invoked. The throw *is* seen by a registered `IRequestExceptionHandler<,>` — the pre-processor stage
runs inside the guard — so a validation or authorization pre-processor can throw and have the
exception handler turn it into a response. If none suppresses it, it propagates and the
post-processors are skipped.

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

Run after the pipeline produces a response. They are skipped whenever the dispatch fails — a throw
from a pre-processor, from a behavior or from the handler bypasses them all.

They **do** run in two cases where the handler itself never returned: when an
`IRequestExceptionHandler<,>` suppresses the exception, and when a behavior short-circuits the chain
without calling `next`. In both cases they receive the substituted response, so an auditing
post-processor still fires on a rejected or cached result. If a post-processor throws, the remaining
ones are skipped and that exception replaces the response. A post-processor throw is not offered to
the exception handlers: the guard stops before this stage.

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
