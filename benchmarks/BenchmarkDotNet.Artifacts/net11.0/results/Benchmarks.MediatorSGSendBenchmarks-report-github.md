```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.3 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                 |  1.864 ns | 0.0049 ns | 0.0044 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Send_3Behaviors | 23.562 ns | 0.0743 ns | 0.0695 ns | 12.64 |    0.05 |    2 |         - |          NA |
| MediatorSG_Send_5Behaviors | 29.064 ns | 0.0651 ns | 0.0609 ns | 15.59 |    0.05 |    3 |         - |          NA |
