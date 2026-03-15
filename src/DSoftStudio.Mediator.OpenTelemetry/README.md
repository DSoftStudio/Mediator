<p align="center">
  <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="200">
</p>

# DSoftStudio.Mediator.OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.OpenTelemetry.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.OpenTelemetry)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

OpenTelemetry instrumentation for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). Provides automatic distributed tracing and metrics for all mediator operations via standard pipeline behaviors.

## Features

- **Distributed tracing** — Activity spans for `Send`, `Publish`, and `CreateStream` with semantic attributes
- **Metrics** — Histogram metrics for request duration and counters for operations
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
    options.RecordMetrics = true;
    options.Filter = type => !type.Name.Contains("HealthCheck");
});
```

## Documentation

📖 [Full documentation](https://github.com/DSoftStudio/Mediator/blob/main/docs/integrations/opentelemetry.md)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
