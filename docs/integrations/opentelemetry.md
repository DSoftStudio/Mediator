# OpenTelemetry Instrumentation

The companion package **`DSoftStudio.Mediator.OpenTelemetry`** provides automatic distributed tracing and metrics for all mediator operations — zero changes to existing code.

```shell
dotnet add package DSoftStudio.Mediator.OpenTelemetry
```

## Registration

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorInstrumentation()       // ← registers tracing + metrics behaviors
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

// Optional: wire into the OTel SDK
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddMediatorInstrumentation())    // ← subscribes to ActivitySource
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMediatorInstrumentation());   // ← subscribes to Meter
```

## What You Get

| Signal | Details |
|---|---|
| **Tracing** | One span per `Send()`, `Publish()`, `CreateStream()` with CQRS-aware naming (`CreateUser command`, `GetUsers query`). Per-handler child spans for notifications. |
| **Metrics** | `mediator.request.duration` (histogram), `mediator.request.active` (up-down counter), `mediator.request.errors` (counter with `error.type` tag). |
| **Zero-cost when unused** | `HasListeners()` short-circuit adds ~1 ns when no exporter is configured. |

## Configuration

```csharp
services.AddMediatorInstrumentation(options =>
{
    options.EnableTracing = true;      // default
    options.EnableMetrics = true;      // default

    // Suppress instrumentation for specific types
    options.Filter = type => !type.Name.StartsWith("HealthCheck");

    // Add custom tags
    options.EnrichActivity = (activity, request) =>
    {
        if (request is IHasTenantId t)
            activity.SetTag("tenant.id", t.TenantId);
    };

    options.RecordExceptionStackTraces = true; // default
});
```

Works with any OTel-compatible backend: Jaeger, Zipkin, OTLP, Aspire Dashboard, etc. See [ADR-0004](../adr/0004-opentelemetry-instrumentation.md) for the full design rationale.
