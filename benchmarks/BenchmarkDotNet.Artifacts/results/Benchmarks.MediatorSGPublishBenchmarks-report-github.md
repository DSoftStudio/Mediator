```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish     |  4.150 ns | 0.0486 ns | 0.0454 ns |  1.00 |    0.01 |    1 |         - |          NA |
| MediatorSG_Publish | 10.602 ns | 0.0212 ns | 0.0198 ns |  2.55 |    0.03 |    2 |         - |          NA |
