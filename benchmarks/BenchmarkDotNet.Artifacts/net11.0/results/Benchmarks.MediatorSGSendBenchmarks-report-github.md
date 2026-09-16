```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.34 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                     | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                 |  1.731 ns | 0.0065 ns | 0.0097 ns |  1.730 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Send_3Behaviors | 19.421 ns | 0.0244 ns | 0.0357 ns | 19.419 ns | 11.22 |    0.06 |    2 |         - |          NA |
| MediatorSG_Send_5Behaviors | 27.229 ns | 0.1944 ns | 0.2909 ns | 27.089 ns | 15.73 |    0.19 |    3 |         - |          NA |
