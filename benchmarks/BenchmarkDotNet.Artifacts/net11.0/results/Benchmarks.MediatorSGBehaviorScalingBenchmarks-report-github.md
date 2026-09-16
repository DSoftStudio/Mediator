```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.29 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      |  1.888 ns | 0.0033 ns | 0.0048 ns |  1.887 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 10.529 ns | 0.0161 ns | 0.0236 ns | 10.536 ns |  5.58 |    0.02 |    2 |         - |          NA |
| Send_1Behaviors | 10.825 ns | 0.0254 ns | 0.0380 ns | 10.820 ns |  5.73 |    0.02 |    3 |         - |          NA |
| Send_2Behaviors | 13.070 ns | 0.1056 ns | 0.1514 ns | 13.018 ns |  6.92 |    0.08 |    4 |         - |          NA |
| Send_3Behaviors | 19.870 ns | 0.0297 ns | 0.0416 ns | 19.858 ns | 10.53 |    0.03 |    5 |         - |          NA |
| Send_5Behaviors | 26.466 ns | 0.0477 ns | 0.0714 ns | 26.459 ns | 14.02 |    0.05 |    6 |         - |          NA |
| Send_8Behaviors | 34.402 ns | 0.0672 ns | 0.1005 ns | 34.374 ns | 18.22 |    0.07 |    7 |         - |          NA |
