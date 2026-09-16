```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 7.21 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                | Mean     | Error     | StdDev    | Median   | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|---------:|------:|-----:|----------:|------------:|
| DirectCall            | 2.113 ns | 0.0058 ns | 0.0087 ns | 2.111 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send_3Behaviors | 5.564 ns | 0.0159 ns | 0.0228 ns | 5.565 ns |  2.63 |    2 |         - |          NA |
| DSoft_Send_5Behaviors | 6.551 ns | 0.0107 ns | 0.0156 ns | 6.554 ns |  3.10 |    3 |         - |          NA |
