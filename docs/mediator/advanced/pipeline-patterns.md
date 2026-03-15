<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Pipeline Patterns

Real-world applications combine multiple pipeline behaviors to implement cross-cutting concerns. This guide shows common patterns used in production with DSoftStudio.Mediator.

## Logging Behavior

Log every request with timing information:

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        _logger.LogInformation("Handling {Request}", name);

        var sw = Stopwatch.StartNew();
        var response = await next.Handle(request, ct);
        sw.Stop();

        _logger.LogInformation("Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
        return response;
    }
}
```

## Transaction Behavior

Wrap commands in a database transaction using the `ICommand` marker:

```csharp
public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly AppDbContext _db;

    public TransactionBehavior(AppDbContext db) => _db = db;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        if (request is not ICommand)
            return await next.Handle(request, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var response = await next.Handle(request, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return response;
    }
}
```

> **Tip:** Use the non-generic `ICommand` marker interface to target only write operations. Queries skip the transaction entirely.

## Authorization Behavior

Enforce permissions before the handler executes:

```csharp
public interface IAuthorizedRequest
{
    string RequiredPermission { get; }
}

public class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUser _user;

    public AuthorizationBehavior(ICurrentUser user) => _user = user;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        if (request is IAuthorizedRequest auth
            && !_user.HasPermission(auth.RequiredPermission))
        {
            throw new UnauthorizedAccessException(
                $"Missing permission: {auth.RequiredPermission}");
        }

        return await next.Handle(request, ct);
    }
}
```

## Retry Behavior (Query-Only)

Retry transient failures for read operations:

```csharp
public class RetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken ct)
    {
        if (request is not IQuery)
            return await next.Handle(request, ct);

        const int maxRetries = 3;
        for (int i = 0; ; i++)
        {
            try
            {
                return await next.Handle(request, ct);
            }
            catch when (i < maxRetries - 1)
            {
                await Task.Delay(100 * (i + 1), ct);
            }
        }
    }
}
```

> **Warning:** Retrying commands can cause duplicate side effects. Only retry idempotent read operations, or use idempotency keys for writes.

## Recommended Registration Order

Pipeline behaviors execute in registration order. A typical production setup:

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddPipelineBehavior(typeof(LoggingBehavior<,>))
    .AddPipelineBehavior(typeof(AuthorizationBehavior<,>))
    .AddPipelineBehavior(typeof(ValidationBehavior<,>))
    .AddPipelineBehavior(typeof(TransactionBehavior<,>))
    .PrecompilePipelines();
```

Execution flow:

```
Request
  → LoggingBehavior       (log entry + timing)
    → AuthorizationBehavior (check permissions)
      → ValidationBehavior  (validate input)
        → TransactionBehavior (wrap in tx if command)
          → Handler         (execute business logic)
```

## See Also

- [Pipeline Behaviors](../features/pipeline-behaviors.md) — core pipeline behavior API
- [CQRS](../concepts/cqrs.md) — `ICommand` / `IQuery` marker interfaces for targeting behaviors
- [Pre/Post Processors](../features/pre-post-processors.md) — simpler before/after hooks
