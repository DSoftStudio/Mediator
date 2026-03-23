```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                       | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      | 679.2 ns | 4.57 ns | 4.27 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatorSG_RealisticPipeline | 718.0 ns | 8.25 ns | 7.72 ns |  1.06 |    2 | 0.0296 |     397 B |        1.47 |
