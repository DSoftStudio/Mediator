![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

# DSoftStudio.Mediator.OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.OpenTelemetry.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

OpenTelemetry instrumentation for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). Provides automatic distributed tracing and metrics for all mediator operations via standard pipeline behaviors.

## Features

- **Distributed tracing** — Activity spans for `Send`, `Publish`, and `CreateStream` with semantic attributes
- **Metrics** — `mediator.request.duration` histogram (seconds, with sub-second buckets) plus `mediator.request.active` / `mediator.request.errors` counters, created from the DI `IMeterFactory`
- **Database dependency enrichment** — automatically tags database spans with a redaction-safe `db.operation.name` / `db.sql.table` / `db.stored_procedure.name`, so each query (`SELECT`, `INSERT`, `CALL`…) shows as its own dependency instead of one aggregated row — no configuration
- **Configurable filtering** — Include or exclude specific request types via `MediatorInstrumentationOptions`
- **Zero configuration** — Works out of the box with any OpenTelemetry exporter

## Installation

```shell
dotnet add package DSoftStudio.Mediator.OpenTelemetry
```

## Quick Start

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorInstrumentation()
    .PrecompilePipelines();

services
    .AddOpenTelemetry()
    .WithTracing(b => b.AddMediatorInstrumentation())
    .WithMetrics(b => b.AddMediatorInstrumentation());
```

## Configuration

```csharp
services.AddMediatorInstrumentation(options =>
{
    // Skip noisy request types (e.g. health checks).
    options.Filter = type => !type.Name.Contains("HealthCheck");

    // Tracing and metrics are both ON by default — turn one off if you only want the other.
    // options.EnableTracing = false;
    // options.EnableMetrics = false;

    // Keep error.type on the span but drop the (verbose) exception stack trace.
    options.RecordExceptionStackTraces = false;
});
```

## Documentation

📖 [Full documentation](https://docs.dsoftstudio.com/mediator/integrations/opentelemetry)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
