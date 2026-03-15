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

Both `SendWelcomeEmail` and `AuditUserCreation` will execute in registration order. Handlers are resolved from DI, so each can have its own dependencies.

## Notification Strategies

By default, handlers run **sequentially** in registration order (if one throws, the rest are skipped). To run all handlers **in parallel**, register the built-in `ParallelNotificationPublisher`:

```csharp
services.AddSingleton<INotificationPublisher, ParallelNotificationPublisher>();
```

| Strategy | Behavior |
|---|---|
| Sequential (default) | Handlers run one at a time. If a handler throws, subsequent handlers are not invoked. |
| `ParallelNotificationPublisher` | All handlers start concurrently via `Task.WhenAll`. If any throw, an `AggregateException` is raised after all complete. |

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
