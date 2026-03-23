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
| `Send()`              |  **7.2 ns** |      12.2 ns  |    33.4 ns  |    41.3 ns  |
| `Send()` (5 behaviors)| **15.6 ns** |      36.8 ns  |    54.1 ns  |   153.1 ns  |
| `Publish()`           |  **4.5 ns** |      10.6 ns  |    35.7 ns  |   123.4 ns  |
| `CreateStream()`      |     45.5 ns |  **44.7 ns**  |    68.1 ns  |   122.9 ns  |
| Cold Start            | **1.62 µs** |     9.91 µs   |   1.88 µs   |    3.24 µs  |

### Key Takeaways

- **Send** is ~1.7× faster than the next fastest (Mediator SG) and ~5.7× faster than MediatR
- **Send with 5 behaviors** shows the pipeline chain stays efficient under load — only ~1.7 ns per behavior hop (8.4 ns total overhead for 5 behaviors)
- **Publish** is ~0.7 ns overhead — effectively a direct call to the handler array
- **Cold Start** at 1.62 µs means precompiled pipelines warm up in under 2 microseconds

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

Full BenchmarkDotNet results and source code are available in the [`/benchmarks`](../benchmarks) folder.

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
