[← Back to Documentation](index.md)

# Benchmarks

All benchmarks run on .NET 10 using [BenchmarkDotNet](https://benchmarkdotnet.org/). Tested against [Mediator](https://github.com/martinothamar/Mediator) 3.0.1 (source-generator based), [DispatchR](https://github.com/AterDev/DispatchR) 2.1.1, and [MediatR](https://github.com/jbogard/MediatR) 14.1.

## Latency

| Operation             | **DSoft**   | Mediator (SG) | DispatchR   | MediatR     |
|-----------------------|------------:|--------------:|------------:|------------:|
| `Send()`              |  **7.1 ns** |      12.5 ns  |    33.4 ns  |    42.1 ns  |
| `Send()` (5 behaviors)| **15.5 ns** |      21.2 ns  |    53.5 ns  |   150.2 ns  |
| `Publish()`           |  **8.5 ns** |      10.2 ns  |    35.0 ns  |   136.1 ns  |
| `CreateStream()`      |     45.8 ns |  **45.3 ns**  |    67.1 ns  |   124.2 ns  |
| Cold Start            | **1.63 µs** |     7.41 µs   |   1.91 µs   |    3.10 µs  |

### Key Takeaways

- **Send** is ~1.8× faster than the next fastest (Mediator SG) and ~6× faster than MediatR
- **Send with 5 behaviors** shows the pipeline chain stays efficient under load — only ~8.4 ns per behavior hop
- **Publish** is ~0.6 ns overhead — effectively a direct call to the handler array
- **Cold Start** at 1.63 µs means precompiled pipelines warm up in under 2 microseconds

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

## Reproducing

Full BenchmarkDotNet results and source code are available in the [`/benchmarks`](../benchmarks) folder.

```shell
dotnet run -c Release --project benchmarks/DSoft.Benchmarks
```

## See Also

- [Performance Design](architecture/performance.md) — explains the techniques behind these numbers
