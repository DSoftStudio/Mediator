```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  8.315 ns | 0.0130 ns | 0.0122 ns |  0.77 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 10.790 ns | 0.0444 ns | 0.0415 ns |  1.00 |    2 |         - |          NA |
