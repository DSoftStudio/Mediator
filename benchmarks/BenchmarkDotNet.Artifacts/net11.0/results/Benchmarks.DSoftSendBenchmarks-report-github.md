```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.22 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DirectCall            | 2.085 ns | 0.0054 ns | 0.0048 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send_3Behaviors | 5.381 ns | 0.0092 ns | 0.0086 ns |  2.58 |    2 |         - |          NA |
| DSoft_Send_5Behaviors | 6.425 ns | 0.0114 ns | 0.0106 ns |  3.08 |    3 |         - |          NA |
