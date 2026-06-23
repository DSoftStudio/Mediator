---
layout: default
title: "OpenTelemetry - DSoftStudio.Mediator"
description: "Distributed tracing and metrics with OpenTelemetry."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

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
| **Metrics** | `mediator.request.duration` (histogram, **seconds**, with sub-second bucket boundaries), `mediator.request.active` (up-down counter), `mediator.request.errors` (counter with `error.type` tag). Instruments are created from the DI `IMeterFactory`. |
| **Database enrichment** | Calling `AddMediatorInstrumentation()` on the `TracerProviderBuilder` also tags database spans with a redaction-safe `db.operation.name` / `db.sql.table` / `db.stored_procedure.name`, so each query (`SELECT`, `INSERT`, `CALL`…) is a distinct dependency instead of one aggregated row. No configuration. |
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

See [ADR-0005](../adr/0005-opentelemetry-instrumentation.md) for the full design rationale.

## See Also

- [Notifications](../concepts/notifications.md) — per-handler child spans for notification dispatch
- [Pipeline Behaviors](../features/pipeline-behaviors.md) — how instrumentation wraps the pipeline
- [Benchmarks](../benchmarks.md) — `HasListeners()` short-circuit adds ~1 ns when no exporter is configured
