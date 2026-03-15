<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Source Generators

Four Roslyn incremental source generators handle all discovery and wiring at build time:

| Generator | Output | Purpose |
|---|---|---|
| `DependencyInjectionGenerator` | `MediatorServiceRegistry.g.cs` | Scans for `IRequestHandler`, `INotificationHandler`, `IStreamRequestHandler`, and self-handling request classes. Emits adapter classes for self-handlers. Registers stateless handlers (no constructor params) as **Singleton**, others as Transient |
| `MediatorPipelineGenerator` | `MediatorRegistry.g.cs` | Registers `PipelineChainHandler<TRequest, TResponse>` as transient for every request type via `PrecompilePipelines()`. Also populates `RequestObjectDispatch` with a delegate per request type for `Send(object)` runtime-typed dispatch |
| `NotificationGenerator` | `NotificationRegistry.g.cs` | Generates static dispatch arrays for each notification type, eliminating runtime service enumeration |
| `StreamGenerator` | `StreamRegistry.g.cs` | Generates static factory delegates for each stream handler |

## Compile-Time Diagnostics

The source generators also emit diagnostics to catch handler misconfigurations at build time:

| Rule | Severity | Description |
|---|---|---|
| **DSOFT001** | Warning | No `IRequestHandler<TRequest, TResponse>` found for a request type |
| **DSOFT002** | Warning | Multiple request handlers for the same `<TRequest, TResponse>` — only the last registered handler executes |
| **DSOFT003** | Warning | Multiple stream handlers for the same `<TRequest, TResponse>` — only the last registered handler executes |

> **Note:** Multiple `INotificationHandler<T>` implementations for the same notification type are expected and do not trigger a diagnostic — notification fan-out is by design.
