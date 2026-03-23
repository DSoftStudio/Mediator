```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DSoft_RealisticPipeline | 666.9 ns | 4.61 ns | 4.31 ns |  0.99 |    1 | 0.0191 |     255 B |        0.94 |
| DirectCall_WithPipeline | 674.1 ns | 3.60 ns | 3.37 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
