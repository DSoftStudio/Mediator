---
layout: default
title: "Benchmarks - DSoftStudio.Mediator"
description: "Performance benchmarks comparing DSoftStudio.Mediator vs MediatR."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](index.md)

# Benchmarks

All benchmarks run on .NET 10 using [BenchmarkDotNet](https://benchmarkdotnet.org/). Tested against [Mediator](https://github.com/martinothamar/Mediator) 3.0.1 (source-generator based), [DispatchR](https://github.com/AterDev/DispatchR) 2.1.1, and [MediatR](https://github.com/jbogard/MediatR) 14.1.

## Latency

| Operation             | **DSoft**   | Mediator (SG) | DispatchR   | MediatR     |
|-----------------------|------------:|--------------:|------------:|------------:|
| `Send()`              |  **2.7 ns** |       9.8 ns  |    27.1 ns  |    40.9 ns  |
| `Send()` (5 behaviors)|  **6.6 ns** |      27.2 ns  |    31.2 ns  |   143.5 ns  |
| `Publish()`           |  **2.4 ns** |       6.3 ns  |    32.3 ns  |   112.9 ns  |
| `CreateStream()`      | **30.7 ns** |      31.8 ns  |    54.0 ns  |   112.8 ns  |

Measured on .NET 11. On .NET 10 the same order holds with everything roughly twice as slow.

**Startup** is a separate table, because it answers a different question and the honest answer is less
flattering. What a library adds to time-to-first-request, over a container holding one trivial service:

| | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|---|---:|---:|---:|---:|
| Added to startup | 22.7 ms | **10.2 ms** | 12.1 ms | 31.5 ms |
| Startup allocation | **32 KB** | 154 KB | 651 KB | 1,680 KB |

This library is third of four on startup time. Most of its share is `PrecompilePipelines()` building the
dispatch chains up front — the work that buys the 2.7 ns dispatch and the 32 KB.

- **Startup** is measured one process per sample: it is a property of a process and cannot be seen from inside a warm one

## Allocations

| Operation             | **DSoft** | Mediator (SG) | DispatchR | MediatR |
|-----------------------|----------:|--------------:|----------:|--------:|
| `Send()`              |    72 B   |        72 B   |    72 B   |   272 B |
| `Send()` (5 behaviors)|    72 B   |        72 B   |    72 B   | 1,088 B |
| `Publish()`           |     0 B   |         0 B   |     0 B   |   768 B |
| `CreateStream()`      |   232 B   |       232 B   |   232 B   |   624 B |

### Key Takeaways

- **72 B per Send** is the `ValueTask<T>` boxing cost — the pipeline itself allocates nothing
- **Adding 5 behaviors does not increase allocation** — interface dispatch chains are zero-alloc
- **Publish is 0 B** — notification dispatch is fully allocation-free
- MediatR allocates **1,088 B with 5 behaviors** due to delegate chains (`RequestHandlerDelegate<T>`)

## Pipeline Allocation Breakdown

| Component | DSoft | MediatR |
|---|---|---|
| Handler resolution | 0 B (singleton) | ~40 B (transient `GetService`) |
| Pipeline chain | 0 B (interface dispatch) | ~160 B per behavior (delegate) |
| Return value | 72 B (`ValueTask` box) | 72 B (`Task` alloc) |
| **5-behavior total** | **72 B** | **1,088 B** |

## Feature Comparison

| Feature                   | DSoft | Mediator (SG) | DispatchR | MediatR |
|---------------------------|:----:|:-------------:|:---------:|:-------:|
| Source generators         | ✔️ | ✔️ | ❌ | ❌ |
| Native AOT compatible     | ✔️ | ✔️ | ❌ | ❌ |
| Reflection-free hot path  | ✔️ | ✔️ | ❌ | ❌ |
| Zero-alloc pipeline       | ✔️ | ✔️ | ✔️ | ❌ |
| Auto-Singleton handlers   | ✔️ | ❌ | ❌ | ❌ |
| Self-handling requests    | ✔️ | ❌ | ❌ | ❌ |
| Runtime-typed `Send(object)` | ✔️ | ❌ | ❌ | ✔️ |
| Compile-time pipeline     | ✔️ | ✔️ | ❌ | ❌ |
| MediatR-style API         | ✔️ | ✔️ | ❌ | ✔️ |

## Realistic Pipeline (Enterprise)

Measures all 4 libraries through a production-representative pipeline: **Validation → Logging → Metrics → async database write** with 3 pipeline behaviors, full DI, and simulated async I/O. Each library has its own `DirectCall_WithPipeline` fair baseline measured in the same isolated BenchmarkDotNet process.

| Benchmark | Latency | Allocated | vs DirectCall |
|---|---:|---:|---:|
| **DSoft** — Realistic Pipeline | 666.9 ns | 255 B | 0.99× |
| **DispatchR** — Realistic Pipeline | 666.7 ns | 255 B | 1.01× |
| **Mediator SG** — Realistic Pipeline | 718.0 ns | 397 B | 1.06× |
| **MediatR** — Realistic Pipeline | 857.1 ns | 1,032 B | 1.20× |

## Reproducing

Full BenchmarkDotNet results and source code are available in the [`/benchmarks`](https://github.com/DSoftStudio/Mediator/tree/main/benchmarks) folder.

```shell
dotnet run -c Release --project benchmarks/DSoftStudio.Mediator.Benchmarks
```

Individual library runs:

```shell
benchmarks/run-dsoft.cmd
benchmarks/run-mediatr.cmd
benchmarks/run-dispatchr.cmd
benchmarks/run-mediatorsg.cmd
```

## See Also

- [Performance Design](architecture/performance.md) — explains the techniques behind these numbers
- [Migration from MediatR](getting-started/migration-from-mediatr.md) — step-by-step guide to switch
- [Pipeline Behaviors](features/pipeline-behaviors.md) — zero-allocation behavior chains explained
- [ADR-0001: Architecture Overview](adr/0001-architecture-overview.md) — full architecture decision record
