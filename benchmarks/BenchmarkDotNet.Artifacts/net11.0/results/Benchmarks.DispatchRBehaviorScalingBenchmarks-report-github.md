```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.32 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      |  2.088 ns | 0.0051 ns | 0.0074 ns |  2.088 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 26.670 ns | 0.0344 ns | 0.0505 ns | 26.665 ns | 12.77 |    0.05 |    2 |         - |          NA |
| Send_1Behaviors | 31.496 ns | 0.0920 ns | 0.1377 ns | 31.498 ns | 15.08 |    0.08 |    3 |         - |          NA |
| Send_2Behaviors | 31.338 ns | 0.0848 ns | 0.1270 ns | 31.301 ns | 15.01 |    0.08 |    3 |         - |          NA |
| Send_3Behaviors | 31.951 ns | 0.0825 ns | 0.1183 ns | 31.945 ns | 15.30 |    0.08 |    3 |         - |          NA |
| Send_5Behaviors | 31.824 ns | 0.0824 ns | 0.1207 ns | 31.817 ns | 15.24 |    0.08 |    3 |         - |          NA |
| Send_8Behaviors | 34.227 ns | 0.0978 ns | 0.1464 ns | 34.185 ns | 16.39 |    0.09 |    4 |         - |          NA |
