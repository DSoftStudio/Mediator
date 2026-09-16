---
layout: default
title: "Native AOT - DSoftStudio.Mediator"
description: "Native AOT and trimming: what is verified, and what needs a step from you."
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

DSoftStudio.Mediator publishes and runs as a Native AOT binary. Verified rather than declared: a native
executable was published and executed, and typed `Send`, `Send(object)`, `Publish`, streams and pipeline
behaviors all dispatch correctly — including responses with **value types**, which is the case that
breaks a reflection-based mediator outright. Zero `IL2xxx`/`IL3xxx` warnings from ILC. Verified on
win-x64; other runtime identifiers are expected to behave the same but have not been measured.

`DSoftStudio.Mediator` and the three companions set `IsAotCompatible` and `IsTrimmable`, so the trim
analyzer runs on them at build time. `DSoftStudio.Mediator.Abstractions` targets `netstandard2.0`, where
those properties do nothing, so it carries the `[AssemblyMetadata("IsTrimmable", "True")]` marker
directly instead — the same marker the properties would have emitted. The hot execution path uses no reflection, no `MakeGenericType`, no `Expression.Compile`, and no dynamic method generation — all handler discovery and dispatch wiring are performed at compile time by Roslyn source generators.

## Use Cases

This makes the mediator suitable for:

- **Native AOT ASP.NET applications** — publish self-contained, ahead-of-time compiled APIs
- **Serverless / cloud functions** — fast cold start with minimal memory footprint
- **Containerized microservices** — smaller images, no JIT warm-up
- **High-density cloud workloads** — reduced memory per instance

## AOT-Safe Runtime Dispatch

The `Publish(object)` and `Send(object)` overloads are AOT-safe too. `Send(object)` compiles to a generated type switch over the request types the compilation can see; a type it could not see falls through to a pre-built `FrozenDictionary<Type, DispatchDelegate>` dispatch tables populated by the source generator, with no `MakeGenericType` at runtime.

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

Native AOT removes JIT compilation from startup, and on this workload startup is almost entirely JIT:
the same work with everything already compiled runs in 0.81 ms against 78.86 ms cold.

Whole-process wall clock, fresh process per sample, same source published both ways:

| | JIT | Native AOT |
|---|---:|---:|
| Process start to first dispatch | 78.9 ms | **8.3 ms** |
| Published size (console app, self-contained) | 0.3 MB + runtime | 2.7 MB, no runtime needed |

The distributions do not overlap — the slowest AOT sample was 8.85 ms, the fastest JIT one 78.18 ms.

ReadyToRun is the obvious middle ground and does not pay off here: measured on the same source it
removed about a quarter of the startup cost and made every dispatch ~15% slower, permanently, because
methods tiering up out of a precompiled body end up with a less complete profile than the JIT builds
on its own.

## What Makes It AOT-Compatible

| Technique | Why it matters for AOT |
|---|---|
| Source generators (not reflection) | No `Type.GetType()`, no `Assembly.GetTypes()` |
| Generated type switch for `Send(object)` | Ordinary C#, checked by the compiler, no `MakeGenericType` |
| Open-generic behaviors rewritten to closed | The container never constructs a type at runtime |
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

## Two things that need a step from you

Neither is a defect in the mediator, and both are caught at build time rather than in a published
binary — but a Native AOT application has to deal with them.

**A behavior the generator cannot name.** Registering a behavior as an open generic normally costs
nothing under AOT, because the generator removes the open registration and substitutes literal closed
ones. It can only do that for a type it can spell from a separate generated file: not a `file` type,
not a private nested one, not one nested inside a generic. For anything else the open registration
survives, the container has to construct the closed type at runtime, and that throws under AOT when
the response is a value type. **DSOFT011** reports it at the declaration.

**Caching a response type with no serializer.** `HybridCache` serializes everything it caches, and
`AddHybridCache` pre-registers a serializer for exactly two types: `string` and `byte[]`. Everything
else reaches reflection-based `System.Text.Json`, which AOT disables — so a cacheable request whose
response is an ordinary DTO throws on its first dispatch, after the build and the publish both
succeeded. **DSOFT012** names the type at build time. The fix is a `JsonSerializerContext`:

```csharp
[JsonSerializable(typeof(ProductDto))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

services.AddKeyedSingleton<JsonSerializerOptions>(
    typeof(IHybridCacheSerializer<>),
    new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default });
```

Both details matter: without the explicit `<JsonSerializerOptions>` argument the call does not
compile, and a non-keyed registration is silently ignored.

> `[ImmutableObject(true)]` looks like a workaround and is not one. Verified in a native binary, a
> sealed record carrying it throws exactly as an unmarked type does — the serializer-free path needs
> writes disabled on both cache tiers, which caches nothing. What the marker does change is that
> callers receive the same instance instead of a copy, so on a DTO anything mutates it shares that
> mutation for the whole entry lifetime.

## See Also

- [Performance Design](performance.md) — zero-allocation dispatch architecture
- [Source Generators](source-generators.md) — the 5 generators that eliminate runtime reflection
- [Startup benchmark](../benchmarks.md) — what each library adds to time-to-first-request
