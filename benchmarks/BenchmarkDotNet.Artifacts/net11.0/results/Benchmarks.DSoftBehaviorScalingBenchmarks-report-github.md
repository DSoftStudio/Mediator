```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.13 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method          | Mean     | Error     | StdDev    | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|----------:|---------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      | 1.885 ns | 0.0051 ns | 0.0075 ns | 1.884 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 2.471 ns | 0.0034 ns | 0.0048 ns | 2.470 ns |  1.31 |    0.01 |    2 |         - |          NA |
| Send_1Behaviors | 4.433 ns | 0.0097 ns | 0.0145 ns | 4.431 ns |  2.35 |    0.01 |    3 |         - |          NA |
| Send_2Behaviors | 5.048 ns | 0.0146 ns | 0.0219 ns | 5.047 ns |  2.68 |    0.02 |    4 |         - |          NA |
| Send_3Behaviors | 5.576 ns | 0.0103 ns | 0.0145 ns | 5.576 ns |  2.96 |    0.01 |    5 |         - |          NA |
| Send_5Behaviors | 6.611 ns | 0.0542 ns | 0.0742 ns | 6.583 ns |  3.51 |    0.04 |    6 |         - |          NA |
| Send_8Behaviors | 9.270 ns | 0.0110 ns | 0.0157 ns | 9.267 ns |  4.92 |    0.02 |    7 |         - |          NA |
