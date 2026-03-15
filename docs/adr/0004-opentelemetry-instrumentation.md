# ADR-0004: OpenTelemetry Instrumentation Package

## Status

**Accepted**

## Date

2025-07-19

## Context

Production systems using DSoftStudio.Mediator currently have no built-in observability.
Teams must write custom `IPipelineBehavior<,>` implementations for tracing and metrics,
leading to inconsistent instrumentation across projects: different span names, missing
error tags, no standardized metrics, and no correlation between mediator operations and
the surrounding HTTP/gRPC trace context.

OpenTelemetry is the industry standard for observability in .NET. The official
`OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Instrumentation.Http`
libraries demonstrate the pattern: a separate NuGet package that instruments an existing
library without modifying its core. This ADR follows the same approach.

### Current state

- No tracing: `Send()`, `Publish()`, and `CreateStream()` produce no `Activity` spans
- No metrics: no request duration, throughput, or error rate counters
- Custom pipeline behaviors are the only option — each team writes their own
- No correlation between mediator dispatch and the parent HTTP request trace

### What the ecosystem does

| Library | Instrumentation approach |
|---|---|
| `OpenTelemetry.Instrumentation.AspNetCore` | Separate NuGet, `ActivitySource`, `Meter` |
| `OpenTelemetry.Instrumentation.Http` | Separate NuGet, `DiagnosticListener` hooks |
| MassTransit | Built into core — `ActivitySource` in `ConsumeContext` |
| MediatR | Nothing built-in — community writes pipeline behaviors |
| Mediator (SG) | Nothing built-in |

## Decision

Create a new NuGet package **`DSoftStudio.Mediator.OpenTelemetry`** as a project within
the existing solution (same repository, same CI). The package provides automatic
distributed tracing and metrics for all mediator operations via standard
`IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>`, and an `INotificationPublisher`
decorator — zero changes to the core mediator library.

### 1. Package structure

```
src/DSoftStudio.Mediator.OpenTelemetry/
├── DSoftStudio.Mediator.OpenTelemetry.csproj
├── MediatorInstrumentation.cs              ← ActivitySource + Meter definitions
├── MediatorTracingBehavior.cs              ← IPipelineBehavior (tracing)
├── MediatorMetricsBehavior.cs              ← IPipelineBehavior (metrics)
├── MediatorStreamTracingBehavior.cs        ← IStreamPipelineBehavior (tracing)
├── MediatorStreamMetricsBehavior.cs        ← IStreamPipelineBehavior (metrics)
├── InstrumentedNotificationPublisher.cs    ← INotificationPublisher decorator (tracing + metrics)
├── MediatorInstrumentationOptions.cs       ← Configuration options
├── ServiceCollectionExtensions.cs          ← AddMediatorInstrumentation()
└── TracerProviderBuilderExtensions.cs      ← AddMediatorInstrumentation() for OTel SDK

tests/DSoftStudio.Mediator.OpenTelemetry.Tests/
├── TracingBehaviorTests.cs
├── MetricsBehaviorTests.cs
├── StreamTracingBehaviorTests.cs
├── StreamMetricsBehaviorTests.cs
├── NotificationPublisherTests.cs
├── RegistrationTests.cs
└── FilteringTests.cs

samples/opentelemetry/
├── DSoft.Sample.OpenTelemetry.Api/
└── DSoft.Sample.OpenTelemetry.Application/
```

### 2. NuGet dependencies

```xml
<!-- The package references only the API surface — not the full SDK.
     Consumers bring their own exporter (Jaeger, OTLP, Console, etc.) -->
<PackageReference Include="OpenTelemetry.Api" Version="1.15.0" />

<!-- Already part of the shared framework in .NET 8+, but explicit
     for clarity and netstandard2.0 fallback if ever needed -->
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="10.0.5"
                  Condition="'$(TargetFramework)' == 'netstandard2.0'" />
```

The package depends on `DSoftStudio.Mediator` (project reference in source, NuGet
dependency in the published package) and `OpenTelemetry.Api` (the lightweight API
contract — not the full SDK). This keeps the dependency footprint minimal.

### 3. Telemetry signals

#### 3.1 Distributed tracing (`ActivitySource`)

A single `ActivitySource` named `"DSoftStudio.Mediator"` with version matching the
package version. Each mediator operation starts a child `Activity` under the current
ambient trace context.

**Span naming convention** (follows [OpenTelemetry Semantic Conventions for Messaging](https://opentelemetry.io/docs/specs/semconv/messaging/)):

| Operation | Span name | `ActivityKind` |
|---|---|---|
| `Send(new CreateUser(...))` | `CreateUser send` | `Internal` |
| `Publish(new UserCreated(...))` | `UserCreated publish` | `Internal` |
| `CreateStream(new StreamNumbers())` | `StreamNumbers stream` | `Internal` |

**Why `{TypeName} {operation}` and not `mediator.send`:**

The OTel messaging semantic conventions specify span names as
`{messaging.destination.name} {messaging.operation.name}` — for example, `orders send`,
`orders receive`. In the mediator context, the "destination" is the request type and
the "operation" is the dispatch verb. Using a flat `mediator.send` for all requests
would:

- Make trace waterfall views useless — every span looks identical until you expand tags
- Contradict how ASP.NET Core (`GET /api/users`), gRPC (`package.Service/Method`), and
  HTTP (`GET`) instrumentation all use operation-specific span names
- Force users to configure tag-based grouping in every dashboard

The `{TypeName} {operation}` pattern produces immediately readable traces while keeping
metric aggregation clean through tag dimensions (all Send operations share
`mediator.request.kind=command` regardless of span name).

**Span attributes:**

| Attribute | Type | Example | Description |
|---|---|---|---|
| `mediator.request.type` | string | `"MyApp.CreateUser"` | Full type name of the request/notification |
| `mediator.response.type` | string | `"System.Guid"` | Full type name of `TResponse` |
| `mediator.request.kind` | string | `"command"` / `"query"` / `"request"` / `"notification"` / `"stream"` | Detected via `ICommand` / `IQuery` marker interfaces |
| `mediator.pipeline.has_behaviors` | boolean | `true` | Whether the request has pipeline behaviors |
| `error.type` | string | `"System.InvalidOperationException"` | Set on span status = Error (OTel convention) |

**Span status:**

- `Ok` when the handler completes successfully
- `Error` when an exception propagates (exception recorded on the span via `Activity.RecordException`)

**Activity events:**

- `mediator.handler.start` — recorded when the innermost handler begins (after all behaviors)
- `mediator.exception` — recorded when an exception occurs, with `exception.type`, `exception.message`, and `exception.stacktrace` attributes

#### 3.2 Metrics (`Meter`)

A single `Meter` named `"DSoftStudio.Mediator"`. Three instruments:

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `mediator.request.duration` | Histogram | `s` (seconds) | Time from behavior entry to handler completion (includes full pipeline) |
| `mediator.request.active` | UpDownCounter | `{request}` | Number of in-flight requests (increment on start, decrement on complete) |
| `mediator.request.errors` | Counter | `{error}` | Count of failed requests (exception escaped the pipeline) |

**Metric dimensions (tags):**

| Tag | Applied to | Description |
|---|---|---|
| `mediator.request.type` | All 3 | Full type name of the request |
| `mediator.request.kind` | All 3 | `"command"` / `"query"` / `"request"` / `"notification"` / `"stream"` |
| `error.type` | `errors` only | Exception type name |

### 4. Registration API

#### 4.1 DI registration (minimal — no OTel SDK dependency)

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorInstrumentation()       // ← registers behaviors
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

`AddMediatorInstrumentation()` registers:

- `MediatorTracingBehavior<,>` as open-generic `IPipelineBehavior<,>` (first position — outermost)
- `MediatorMetricsBehavior<,>` as open-generic `IPipelineBehavior<,>` (second position)
- `MediatorStreamTracingBehavior<,>` as open-generic `IStreamPipelineBehavior<,>`
- `MediatorStreamMetricsBehavior<,>` as open-generic `IStreamPipelineBehavior<,>`
- `InstrumentedNotificationPublisher` as `INotificationPublisher` decorator (wraps the
  existing publisher — sequential or parallel — with per-handler child spans)

#### 4.2 OTel SDK integration (optional — for `TracerProviderBuilder`)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddMediatorInstrumentation())    // ← subscribes to ActivitySource
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMediatorInstrumentation());   // ← subscribes to Meter
```

These extension methods call `AddSource("DSoftStudio.Mediator")` and
`AddMeter("DSoftStudio.Mediator")` respectively. They are convenience methods —
the user can also call `AddSource`/`AddMeter` directly.

### 5. Configuration options

```csharp
services.AddMediatorInstrumentation(options =>
{
    // Disable tracing or metrics independently
    options.EnableTracing = true;    // default: true
    options.EnableMetrics = true;    // default: true

    // Filter: skip instrumentation for specific request types
    options.Filter = (requestType) =>
    {
        // Return false to suppress instrumentation for this type
        return !requestType.Name.StartsWith("Health");
    };

    // Enrich: add custom tags to the Activity
    options.EnrichActivity = (activity, request) =>
    {
        if (request is IHasTenantId tenantAware)
            activity.SetTag("tenant.id", tenantAware.TenantId);
    };

    // Control whether to record exception stack traces on spans
    options.RecordExceptionStackTraces = true; // default: true
});
```

### 6. Behavior implementation strategy

#### 6.1 Per-type metadata cache

The CLR creates one static field set per closed generic type — `MediatorTelemetryMetadata<Ping, int>`
is a separate type from `MediatorTelemetryMetadata<CreateUser, Guid>`. Fields are initialized once
on first access (amortized into the type's static constructor) and subsequently read as direct
field loads (~1 ns).

```csharp
internal static class MediatorTelemetryMetadata<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Cached span name: "{TypeName} {kind}". Avoids per-call string interpolation allocation.</summary>
    public static readonly string SpanName = $"{typeof(TRequest).Name} {RequestKind}";

    /// <summary>Full type name for the mediator.request.type tag.</summary>
    public static readonly string RequestType = typeof(TRequest).FullName!;

    /// <summary>Full type name for the mediator.response.type tag.</summary>
    public static readonly string ResponseType = typeof(TResponse).FullName!;

    /// <summary>"command" | "query" | "request" — detected once via IsAssignableFrom.</summary>
    public static readonly string RequestKind = DetectKind();

    private static string DetectKind()
    {
        if (typeof(ICommand).IsAssignableFrom(typeof(TRequest))) return "command";
        if (typeof(IQuery).IsAssignableFrom(typeof(TRequest)))   return "query";
        return "request";
    }
}
```

**What this eliminates per request:**

| Per-call cost (without cache) | Cost | With cache |
|---|---|---|
| `$"{typeof(TRequest).Name} {kind}"` — string interpolation | ~20-40 ns + heap allocation | Static field load (~1 ns), zero alloc |
| `DetectKind(request)` — 2× `isinst` interface cast checks | ~3-5 ns | Already computed |
| `typeof(TRequest).FullName` — vtable dispatch × 2 | ~2-4 ns | Already computed |
| **Total tag computation** | **~25-50 ns + allocation** | **~3 ns, zero alloc** |

> **Note:** `typeof(TRequest).FullName` is already cached internally by the CLR
> (`RuntimeType.GetCachedName`). The metadata cache saves the vtable dispatch through
> the `Type` abstract property, not a full recomputation. The primary win is eliminating
> the **SpanName string interpolation** allocation.

> **Rejected alternative — source-generated const metadata:** A source generator could
> emit `const string` fields per request type, eliminating even the one-time static
> constructor cost. This was rejected because: (1) the static ctor cost is microseconds
> and happens once per type per AppDomain, (2) it would require a second source generator
> in the OTel package with cross-assembly dependency on the core generators, and (3) the
> CLR's generic specialization already achieves the same O(1) field access.

#### 6.2 Tracing behavior

```csharp
public sealed class MediatorTracingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;
    private readonly MediatorInstrumentationOptions _options;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Fast exit: ~1 ns bool check when no exporter is configured.
        if (!Source.HasListeners())
            return await next.Handle(request, cancellationToken);

        // Filter: delegate invocation (~5 ns) — not cached because filter
        // may depend on runtime state (feature flags, config reload, etc.)
        if (_options.Filter is not null && !_options.Filter(typeof(TRequest)))
            return await next.Handle(request, cancellationToken);

        // All metadata reads are static field loads (~1 ns each, zero alloc).
        using var activity = Source.StartActivity(
            MediatorTelemetryMetadata<TRequest, TResponse>.SpanName,
            ActivityKind.Internal);

        // IsAllDataRequested is false when the OTel sampler drops this activity.
        // Skip SetTag to avoid the internal tag list bookkeeping on sampled-out spans.
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("mediator.request.type", MediatorTelemetryMetadata<TRequest, TResponse>.RequestType);
            activity.SetTag("mediator.response.type", MediatorTelemetryMetadata<TRequest, TResponse>.ResponseType);
            activity.SetTag("mediator.request.kind", MediatorTelemetryMetadata<TRequest, TResponse>.RequestKind);

            _options.EnrichActivity?.Invoke(activity, request);
        }

        try
        {
            var response = await next.Handle(request, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex, _options.RecordExceptionStackTraces);
            throw;
        }
    }
}
```

#### 6.3 Zero-cost when no listener

The `ActivitySource.HasListeners()` check is the standard OTel pattern. When no
exporter is configured (production hot path without tracing), the behavior short-circuits
with a single `bool` check (~1 ns) — **no `Activity` object allocated, no tags set**.

This is critical for preserving the mediator's ~7 ns Send latency when tracing is disabled.

### 7. Performance budget

| Scenario | Additional latency | Additional allocation | Notes |
|---|---|---|---|
| **No OTel SDK** (no listeners) | ~1 ns (bool check) | 0 B | `HasListeners()` returns false → pass-through |
| **OTel SDK active, sampled out** | ~5 ns | 0 B | `HasListeners()` true, `StartActivity()` returns null |
| **OTel SDK active, tracing only** | ~200-400 ns | ~400-600 B | `Activity` allocation + `SetTag` bookkeeping |
| **OTel SDK active, tracing + metrics** | ~300-500 ns | ~400-600 B | Histogram record adds ~100-200 ns |

**Cost floor:** `Activity.StartActivity()` (~100-200 ns) and `Activity.Dispose()` (~50-100 ns)
are the dominant costs and cannot be optimized — they are intrinsic to the OTel API. The
metadata cache (§6.1) eliminates the per-call string interpolation and interface cast checks
that would otherwise add ~25-50 ns + a heap allocation per request.

Context: In production systems using OpenTelemetry, the HTTP request itself costs
~1-5 μs in ASP.NET Core middleware. The ~300-500 ns mediator instrumentation overhead
is <10% of the surrounding HTTP trace — negligible in practice.

**Key principle:** The behaviors are registered as `IPipelineBehavior<,>` — users who
don't install the OTel package pay zero cost. Users who install it but don't configure
an exporter pay ~1 ns per request (bool check). Only users who actively export traces
pay the full instrumentation cost.

### 8. Project placement

Same repository, same solution, new project under `src/`:

**Rationale:**
- Single CI pipeline validates compatibility on every commit
- Atomic PRs: core changes + OTel behavior updates in one review
- Shared `Directory.Build.props` for consistent build settings
- Same release cadence — version stays in sync with the core package

The project produces a **separate NuGet package** (`DSoftStudio.Mediator.OpenTelemetry`)
with a NuGet dependency on `DSoftStudio.Mediator` (not a project reference in the
published package).

### 9. Target framework

```xml
<TargetFramework>net8.0</TargetFramework>
```

Rationale:
- `System.Diagnostics.Activity` and `ActivitySource` are stable and feature-complete on net8.0+
- `OpenTelemetry.Api` 1.15.0 targets `net8.0` and `netstandard2.0`
- The core `DSoftStudio.Mediator` package targets `net8.0` — no need to go broader
- Keeps the project simple: no conditional compilation, no polyfills

### 10. Naming conventions

Following the OpenTelemetry .NET instrumentation naming pattern:

| Official library | Our equivalent |
|---|---|
| `OpenTelemetry.Instrumentation.AspNetCore` | `DSoftStudio.Mediator.OpenTelemetry` |
| `AddAspNetCoreInstrumentation()` | `AddMediatorInstrumentation()` |
| `ActivitySource("Microsoft.AspNetCore")` | `ActivitySource("DSoftStudio.Mediator")` |
| `Meter("Microsoft.AspNetCore")` | `Meter("DSoftStudio.Mediator")` |

### 11. Notification handler instrumentation

Notifications use `INotificationHandler<T>`, not `IPipelineBehavior<,>`. To instrument
individual handler execution, the package provides an `InstrumentedNotificationPublisher`
that wraps the user's existing publisher (sequential or parallel) as a decorator:

```csharp
internal sealed class InstrumentedNotificationPublisher : INotificationPublisher
{
    private readonly INotificationPublisher _inner; // the original publisher
    private readonly MediatorInstrumentationOptions _options;

    public async Task Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        if (!MediatorInstrumentation.ActivitySource.HasListeners())
        {
            await _inner.Publish(handlers, notification, cancellationToken);
            return;
        }

        using var parentActivity = MediatorInstrumentation.ActivitySource
            .StartActivity($"{typeof(TNotification).Name} publish", ActivityKind.Internal);

        if (parentActivity is { IsAllDataRequested: true })
        {
            parentActivity.SetTag("mediator.request.type", typeof(TNotification).FullName);
            parentActivity.SetTag("mediator.request.kind", "notification");
        }

        // Wrap each handler to create child spans
        var instrumented = handlers.Select(h => new InstrumentedHandler<TNotification>(h, _options));
        await _inner.Publish(instrumented, notification, cancellationToken);
    }
}
```

This produces a trace tree like:

```
UserCreated publish                          ← parent span
   ├── SendWelcomeEmail handle               ← child span per handler
   ├── AuditUserCreation handle              ← child span per handler
   └── UpdateSearchIndex handle              ← child span per handler
```

**Design:**
- The decorator wraps the user's publisher (sequential or parallel) — it does not replace
  the execution strategy. Parallel publishers still run handlers in parallel; the decorator
  only adds tracing.
- `AddMediatorInstrumentation()` replaces the existing `INotificationPublisher` registration
  with the decorator. If no custom publisher is registered, it wraps the default sequential
  publisher.
- When `HasListeners()` is false, the decorator delegates directly to the inner publisher
  with no wrapping — zero overhead.

### 12. Stream duration trade-off

Stream spans (`CreateStream`) cover the **entire enumeration lifetime** — from the first
`MoveNextAsync()` to the last. For long-running streams (event feeds, progressive
responses), this can produce very long spans (minutes or hours).

This is intentional:
- The span captures the full consumer-side duration, which is the meaningful metric
- Short-lived streams (most common) produce normal-duration spans
- Long-running streams can be excluded via `options.Filter`
- Per-element spans would create unbounded span counts and extreme cardinality

A future option (`options.StreamSpanStrategy = StreamSpanStrategy.PerElement`) could be
added if there is demand, but the default is full-enumeration spans.

### 13. What this ADR does NOT cover

- **Sampling configuration:** Sampling is the responsibility of the OTel SDK, not the
  instrumentation library. Users configure sampling via `TracerProviderBuilder.SetSampler()`.

- **Log correlation:** `Activity.Current` is automatically propagated to `ILogger` scopes
  via `Microsoft.Extensions.Logging`. No special handling needed — mediator tracing spans
  automatically appear in structured logs if the user has logging configured.

## Consequences

### Positive

- Zero-config observability for all mediator operations
- Follows the established OpenTelemetry .NET instrumentation pattern
- Zero overhead when no OTel SDK is configured (`HasListeners()` short-circuit)
- Separate NuGet package — no dependency added to the core mediator
- Works with any OTel-compatible backend (Jaeger, Zipkin, OTLP, Aspire Dashboard, etc.)
- CQRS-aware: spans are tagged with `command` / `query` / `request` kind
- Notification per-handler child spans — unique among .NET mediator libraries
- Enrichment hooks for custom tags (tenant ID, correlation ID, etc.)
- Filtering hooks to suppress noisy request types (health checks, etc.)
- `IsAllDataRequested` guard skips tag allocation on sampled-out spans

### Negative

- One additional NuGet package to maintain and version
- Users must register behaviors **before** `PrecompilePipelines()` (standard registration order)
- Tracing behaviors add ~200-500 ns when active — acceptable for production, but measurable
  in microbenchmarks
- `Activity` allocation when tracing is active (~400-600 B) — unavoidable with the OTel API

### Neutral

- No changes to the core `DSoftStudio.Mediator` package
- No changes to the source generators
- Behaviors are standard `IPipelineBehavior<,>` — they participate in the existing pipeline
  chain with no special treatment
- Notification dispatch (`Publish`) gets a parent span with per-handler child spans via
  the `InstrumentedNotificationPublisher` decorator — no changes to the core `Mediator.cs`
