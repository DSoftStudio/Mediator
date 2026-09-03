```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  8.348 ns | 0.0435 ns | 0.0407 ns |  0.76 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 11.052 ns | 0.0379 ns | 0.0355 ns |  1.00 |    2 |         - |          NA |
