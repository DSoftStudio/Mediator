```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                    | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   | 713.9 ns | 3.65 ns | 3.41 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatR_RealisticPipeline | 857.1 ns | 2.38 ns | 2.23 ns |  1.20 |    2 | 0.0782 |    1032 B |        3.82 |
