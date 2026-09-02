---
layout: default
title: "Notifications - DSoftStudio.Mediator"
description: "Publish notifications to multiple handlers with zero-allocation dispatch."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Notifications

Notifications are dispatched to all registered handlers sequentially. Multiple handlers can subscribe to the same notification type, enabling decoupled event-driven architectures.

> **Design note:** DSoftStudio.Mediator dispatches notifications by exact type (compile-time), not by inheritance hierarchy (runtime reflection). This is a deliberate design decision that avoids the MediatR duplicate handler bug.

```csharp
public record UserCreated(Guid Id) : INotification;

public class SendWelcomeEmail : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken ct)
    {
        Console.WriteLine($"Sending welcome email to {notification.Id}");
        return Task.CompletedTask;
    }
}

public class AuditUserCreation : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken ct)
    {
        Console.WriteLine($"Audit: user {notification.Id} created");
        return Task.CompletedTask;
    }
}
```

Publish a notification:

```csharp
await mediator.Publish(new UserCreated(userId));
```

Both `SendWelcomeEmail` and `AuditUserCreation` will execute, one after another. Handlers are resolved from DI, so each can have its own dependencies.

> **Do not depend on the order between handlers.** The generated dispatch table and the generated
> container registrations are both ordered by handler *type name*, not by the order the registrations
> appear in. The sequence is deterministic — the two dispatch routes agree — but it is not the one
> your registration code suggests, and renaming a handler changes it. Work that must happen in a
> given sequence belongs in a single handler, or behind a request.

## Notification Strategies

By default, handlers run **sequentially** — each one completes before the next starts — and if one
throws, the rest are skipped. To let them overlap, register the built-in
`ParallelNotificationPublisher`:

```csharp
services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();
```

| Strategy | Behavior |
|---|---|
| Sequential (default) | Handlers run one at a time. If a handler throws, subsequent handlers are not invoked. |
| `ParallelNotificationPublisher` | Queues every handler to the thread pool, then awaits them together with `Task.WhenAll`. |

Two things about the parallel publisher are worth knowing:

- **Your handlers must be thread-safe with respect to each other.** Each one is queued to the thread
  pool, so they run concurrently whether or not they suspend — including handlers written in the
  recommended `return Task.CompletedTask` style. Shared state they touch needs synchronizing. The
  cost of this is one thread-pool work item per handler; the default path has none.
- **`await` surfaces one exception, not an `AggregateException`.** Awaiting the task returned by
  `Publish` rethrows the first faulted handler's exception. Inspect `Task.Exception` on the
  un-awaited task if you need them all. Every handler is started regardless, including after one
  fails — a handler that throws synchronously does not stop the others.

Registering any `INotificationPublisher` — including the built-in `SequentialNotificationPublisher`,
which reproduces the default semantics — bypasses the generated fast path. Handlers are then
resolved through `IEnumerable<INotificationHandler<T>>` instead of the generated concrete factories,
which costs a container lookup per publish and yields a *different* singleton instance for a handler
that keeps state.

That also changes **which** handlers run. The default path dispatches the table the generator built at
compile time; a publisher is handed whatever the container returns. A handler the generator could not
see — registered by hand against `INotificationHandler<T>` — is skipped by default and invoked once a
publisher is registered.

## Custom Strategies

You can also implement `INotificationPublisher` for custom strategies (fire-and-forget, batched, prioritized, etc.):

```csharp
public class FireAndForgetPublisher : INotificationPublisher
{
    public Task Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        foreach (var handler in handlers)
            _ = handler.Handle(notification, cancellationToken);

        return Task.CompletedTask;
    }
}
```

## See Also

- [Design Notes](../architecture/design-notes.md) — why exact-type dispatch was chosen over inheritance scanning
- [ADR-0002: Handler Discovery](../adr/0002-handler-discovery-and-bug-avoidance.md) — avoiding the MediatR duplicate handler bug
- [OpenTelemetry](../integrations/opentelemetry.md) — automatic tracing for notification dispatch
