---
layout: default
title: "Source Generators - DSoftStudio.Mediator"
description: "Roslyn incremental source generators for handler discovery."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Source Generators

Five Roslyn incremental source generators handle all discovery and wiring at build time:

| Generator | Output | Purpose |
|---|---|---|
| `DependencyInjectionGenerator` | `MediatorServiceRegistry.g.cs` | Scans for `IRequestHandler`, `INotificationHandler`, `IStreamRequestHandler`, and self-handling request classes. Emits adapter classes for self-handlers. Registers stateless handlers (no constructor params) as **Singleton**, others as Transient |
| `MediatorPipelineGenerator` | `MediatorRegistry.g.cs` | Registers `PipelineChainHandler<TRequest, TResponse>` as transient for every request type via `PrecompilePipelines()`. Also populates `RequestObjectDispatch` with a delegate per request type for `Send(object)` runtime-typed dispatch |
| `MediatorExtensionsGenerator` | `MediatorExtensions.g.cs` | Generates typed extension methods on `ISender` / `IMediator` (e.g. `Send(Ping)`, `CreateStream(PingStream)`) so the compiler infers both generic type parameters. Uses defensive `isinst` + virtual-dispatch fallback (never branchless castclass) to ensure correct behavior in test projects, mocking scenarios, and `dotnet test -c Release` pipelines |
| `NotificationGenerator` | `NotificationRegistry.g.cs` | Generates static dispatch arrays for each notification type, eliminating runtime service enumeration |
| `StreamGenerator` | `StreamRegistry.g.cs` | Generates static factory delegates for each stream handler |

## Compile-Time Diagnostics

The source generators also emit diagnostics to catch handler misconfigurations at build time:

| Rule | Severity | Description |
|---|---|---|
| **DSOFT001** | Warning | No `IRequestHandler<TRequest, TResponse>` found for a request type |
| **DSOFT002** | Warning | Multiple request handlers for the same `<TRequest, TResponse>` — only the last registered one executes |
| **DSOFT003** | Warning | Multiple stream handlers for the same pair — only the last registered one executes |
| **DSOFT004** | Warning | A mocking library is referenced while interceptors are enabled — mocks of `ISender` would be bypassed |
| **DSOFT005** | Warning | A handler in a referenced assembly is `internal` and not visible here, so it was skipped |
| **DSOFT006** | Info | Consider `ICommand<T>` / `IQuery<T>` instead of a bare `IRequest<T>` |
| **DSOFT007** | Warning | Redundant registration: `AddMediator(configure)` mixed with the manual chain |
| **DSOFT008** | Warning | `AddMediator()` registered core services but no handlers — dispatch will throw at runtime |
| **DSOFT009** | Warning | A handler was skipped because generated code cannot name it (`file`, private nested, or nested in a generic) |
| **DSOFT010** | Warning | A pipeline component was registered *after* the pipeline scan, so no chain was built for it |
| **DSOFT011** | Warning | A pipeline behavior cannot be named from generated code, so the container closes it at runtime — which throws under Native AOT for a value-type response |
| **DSOFT012** | Warning | A cached response type has no serializer, in a build publishing AOT or trimmed |

> **Note:** Multiple `INotificationHandler<T>` implementations for the same notification type are expected and do not trigger a diagnostic — notification fan-out is by design.

## Interceptor Generators

Three additional generators emit [C# interceptors](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12#interceptors) that replace `ISender.Send`, `IPublisher.Publish`, and `IMediator.CreateStream` call sites at compile time:

| Generator | Intercepts | Key optimization |
|---|---|---|
| `SendInterceptorGenerator` | `ISender.Send<TRequest, TResponse>()` | Branchless `castclass` in Release (GDV-optimized) |
| `PublishInterceptorGenerator` | `IPublisher.Publish<TNotification>()` | `NotificationPublisherFlag` bypass — skips DI probe |
| `StreamInterceptorGenerator` | `IMediator.CreateStream<TRequest, TResponse>()` | Direct static dispatch |

All three interceptor generators adapt to `OptimizationLevel`: Release builds emit branchless `castclass` for JIT GDV; Debug builds emit `isinst` fallback for mock safety. **Typed extensions** (from `MediatorExtensionsGenerator`) always use defensive `isinst` + virtual-dispatch fallback regardless of build configuration — this ensures they work correctly in `dotnet test -c Release` CI pipelines where interceptors may be suppressed. See [Performance Design](performance.md) for details.

### Suppressing interceptors

Set `<DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors>` in your project to disable all interceptor generation — the project falls back to standard virtual dispatch through `IMediator` / `ISender` interfaces.

| Rule | Severity | Description |
|---|---|---|
| **DSOFT004** | Warning | A mocking library (Moq, NSubstitute, FakeItEasy, JustMock, RhinoMocks, NimbleMocks) is referenced with interceptors enabled. In Release builds interceptors use a branchless cast that throws `InvalidCastException` on mock objects |


## Writing a generator against this library

If you are generating code that registers pipeline components, you need to know what the core it is
being compiled against actually does — and a generator cannot ask a library that is not running. The
temptation is to infer capability from a proxy: "type X exists, so behaviour Y is present". That holds
exactly as long as the two happen to have shipped together, and it fails silently when they stop.

`DSoftStudio.Mediator.MediatorCapabilities` says it instead:

```csharp
// In your generator, resolved from the compilation being built.
var caps = compilation.GetTypesByMetadataName("DSoftStudio.Mediator.MediatorCapabilities");

var fold = caps
    .SelectMany(t => t.GetMembers("ChainLifetimeFold"))
    .OfType<IFieldSymbol>()
    .Select(f => f.ConstantValue as int?)
    .FirstOrDefault(v => v is not null) ?? 0;

if (fold >= 1)
{
    // The handler's lifetime constrains the chain's, so a Singleton component is safe here.
}
```

Three details are deliberate, and each one matters to a reader:

- **The plural `GetTypesByMetadataName`.** The singular returns null both when a type is missing and
  when it is declared in more than one referenced assembly. Reading a capability as absent because it
  was ambiguous is the failure this class exists to remove, reintroduced at the last step.
- **Read the value, not the presence.** A marker that can only say "I am here" cannot express a second
  revision of itself. The constants are `int`, so compare with `>=` against the revision you need.
- **`const`, not `static readonly`.** A generator reads `IFieldSymbol.ConstantValue`, which is null for
  a field that only has a value at run time.

These constants live in the **core** package, not in `Abstractions`. The core depends on a minimum
version of `Abstractions` rather than a pinned one, so NuGet can resolve an old core beside a new
`Abstractions` — a capability declared there would be describing a core that may not have it.

Each constant is covered by a test that exercises the behaviour it claims rather than the constant, so
a capability cannot be left advertised after the core stops providing it.
