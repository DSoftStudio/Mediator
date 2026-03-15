<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# ADR-0003: Fail-fast Handler Validation at Startup

## Status

**Released in v1.0.7**

## Context

DI-based mediators (including DSoftStudio.Mediator) can fail to resolve handlers at runtime if a handler or its dependencies throw during construction. By default, this failure is only detected when the handler is first resolved (e.g., on the first request), which can make misconfiguration hard to diagnose and affect unrelated requests. A fail-fast pattern is desired: all handlers should be validated at startup, and the app should fail immediately if any handler cannot be constructed.

## Decision

DSoftStudio.Mediator provides fail-fast handler validation at startup via a **source-generated** `ValidateMediatorHandlers()` extension method on `IServiceProvider`. This approach was chosen over `IHostedService` to avoid adding a dependency on `Microsoft.Extensions.Hosting.Abstractions` to the core package.

## Implementation

### Architecture

The `DependencyInjectionGenerator` (Roslyn incremental source generator) emits two additional types alongside the existing `MediatorServiceRegistry`:

| Generated Type | Purpose |
|----------------|---------|
| `MediatorHandlerValidator` | Static class with `Validate(IServiceProvider)` — resolves every registered handler from DI |
| `MediatorHandlerValidatorExtensions` | Extension method `ValidateMediatorHandlers(this IServiceProvider)` |

### Validation Strategy

For each handler type discovered at compile time:

| Handler Category | Validation Method | What it validates |
|------------------|-------------------|-------------------|
| `IRequestHandler<TReq, TRes>` | `GetRequiredService<T>()` | Handler + all constructor dependencies |
| `PipelineChainHandler<TReq, TRes>` | `GetService<T>()` (nullable) | Pipeline chain + all behaviors, processors, exception handlers |
| `INotificationHandler<TNotif>` | `GetServices<T>()` + enumerate | All handler implementations for each notification type |
| `IStreamRequestHandler<TReq, TRes>` | `GetRequiredService<T>()` | Stream handler + dependencies |
| `StreamPipelineChainHandler<TReq, TRes>` | `GetService<T>()` (nullable) | Stream pipeline chain if registered |

### Error Aggregation

All failures are collected into a `List<Exception>` and thrown as a single `AggregateException` after all handlers have been validated. This reports **all** misconfigured handlers at once, not just the first failure.

### Usage

```csharp
// ASP.NET Core
var app = builder.Build();
app.Services.ValidateMediatorHandlers(); // Throws AggregateException on failure
app.Run();

// Console / test
var provider = services.BuildServiceProvider();
provider.ValidateMediatorHandlers();
```

### Design Properties

- **Zero new dependencies** — no `Microsoft.Extensions.Hosting.Abstractions` required
- **Fully AOT-safe** — generated code uses concrete types, no reflection, no `MakeGenericType`
- **Zero hot-path impact** — runs once at startup, never on the Send/Publish/CreateStream path
- **Compile-time complete** — validates exactly the handlers the generator discovered
- **Scoped resolution** — creates a DI scope for validation, disposed immediately after

## Consequences

- Misconfiguration or faulty handlers are detected early, improving reliability and diagnosability.
- App startup may take slightly longer if many handlers are registered, but runtime performance is unaffected.
- This pattern is opt-in — users call `ValidateMediatorHandlers()` explicitly.
- Users who prefer `IHostedService` can trivially wrap the call in their own hosted service.
- Handlers with test-only dependencies (e.g., `List<string>`, counters) must have those dependencies registered for validation to pass.

## Test Results

| Test | Result |
|------|--------|
| Happy path (all handlers resolve) | ✅ Pass |
| Missing handlers → `AggregateException` | ✅ Pass |
| Aggregate contains all failures | ✅ Pass |
| Performance regression (allocation) | ✅ No impact — Send=72B, Publish=0B |
| Performance regression (throughput) | ✅ No impact |
| Full test suite (174 tests) | ✅ 0 regressions |

---

## Document History

| Date       | Version | Changes |
|------------|---------|---------|
| —          | Draft   | Initial ADR with implementation plan |
| 2026-03-15 | v1.0.7  | Released with source-generated startup validation |
