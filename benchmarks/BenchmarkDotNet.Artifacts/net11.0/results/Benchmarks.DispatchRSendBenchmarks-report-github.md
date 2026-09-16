```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.32 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                |  1.886 ns | 0.0151 ns | 0.0226 ns |  1.887 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Send_3Behaviors | 31.563 ns | 0.0833 ns | 0.1222 ns | 31.526 ns | 16.74 |    0.21 |    2 |         - |          NA |
| DispatchR_Send_5Behaviors | 31.226 ns | 0.0608 ns | 0.0853 ns | 31.197 ns | 16.56 |    0.20 |    2 |         - |          NA |
