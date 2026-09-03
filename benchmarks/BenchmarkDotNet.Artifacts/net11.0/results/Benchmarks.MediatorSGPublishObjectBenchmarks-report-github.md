```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.34 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                     | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  | 4.327 ns | 0.0128 ns | 0.0113 ns |  0.69 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 6.235 ns | 0.0257 ns | 0.0241 ns |  1.00 |    2 |         - |          NA |
