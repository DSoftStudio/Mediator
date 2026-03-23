---
layout: default
title: "Native AOT - DSoftStudio.Mediator"
description: "Full Native AOT and trimming compatibility."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Native AOT and Trimming

DSoftStudio.Mediator is fully compatible with .NET Native AOT publishing and IL trimming.

Both packages ship with `IsAotCompatible` and `IsTrimmable` enabled, and the trim analyzer is active at build time. The hot execution path uses no reflection, no `MakeGenericType`, no `Expression.Compile`, and no dynamic method generation — all handler discovery and dispatch wiring are performed at compile time by Roslyn source generators.

## Use Cases

This makes the mediator suitable for:

- **Native AOT ASP.NET applications** — publish self-contained, ahead-of-time compiled APIs
- **Serverless / cloud functions** — fast cold start with minimal memory footprint
- **Containerized microservices** — smaller images, no JIT warm-up
- **High-density cloud workloads** — reduced memory per instance

## AOT-Safe Runtime Dispatch

The `Publish(object)` and `Send(object)` overloads (runtime-typed dispatch) are also AOT-safe — they use compile-time generated `FrozenDictionary<Type, DispatchDelegate>` dispatch tables populated by the source generator, with no `MakeGenericType` at runtime.

## Publishing a Native AOT Application

```csharp
// Program.cs — Minimal API with Native AOT
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

var app = builder.Build();
app.Services.ValidateMediatorHandlers();

app.MapPost("/ping", async (IMediator mediator) =>
    await mediator.Send(new Ping()));

app.Run();
```

Publish with:

```shell
dotnet publish -c Release -r linux-x64 /p:PublishAot=true
```

No trimming warnings, no reflection fallbacks, no `rd.xml` configuration needed.

## Cold Start Performance

Native AOT eliminates JIT warm-up entirely. Combined with `PrecompilePipelines()`, the mediator is ready to dispatch on the very first request:

| Metric | JIT (.NET 10) | Native AOT |
|---|---|---|
| Cold start (mediator init) | 1.62 µs | < 1 µs (no JIT) |
| First `Send()` call | Same as warm | Same as warm |
| Binary size (self-contained) | ~80 MB | ~15-25 MB |

## What Makes It AOT-Compatible

| Technique | Why it matters for AOT |
|---|---|
| Source generators (not reflection) | No `Type.GetType()`, no `Assembly.GetTypes()` |
| `FrozenDictionary<Type, Delegate>` | Pre-built dispatch tables, no `MakeGenericType` |
| Interface dispatch (not delegates) | No `Expression.Compile()`, no `DynamicMethod` |
| `ValueTask<T>` returns | No `Task` allocator dependency |
| Static generic specialization | CLR creates dispatch tables per-type at compile time |

## Trimming

Both `DSoftStudio.Mediator` and `DSoftStudio.Mediator.Abstractions` ship with:

```xml
<IsTrimmable>true</IsTrimmable>
<IsAotCompatible>true</IsAotCompatible>
```

The ILLink trim analyzer runs at build time. If your handlers reference types that are not trim-safe, you'll get standard `IL2xxx` warnings — but the mediator infrastructure itself produces zero trimming warnings.

## See Also

- [Performance Design](performance.md) — zero-allocation dispatch architecture
- [Source Generators](source-generators.md) — the 5 generators that eliminate runtime reflection
- [Cold Start Benchmark](../benchmarks.md) — 1.62 µs cold start vs 9.91 µs (Mediator SG) and 3.24 µs (MediatR)
