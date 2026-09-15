```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  2.928 ns | 0.0096 ns | 0.0090 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Publish | 34.906 ns | 0.1459 ns | 0.1218 ns | 11.92 |    0.05 |    2 |         - |          NA |
