```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  7.708 ns | 0.0682 ns | 0.0638 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send            | 13.263 ns | 0.0464 ns | 0.0434 ns |  1.72 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 28.645 ns | 0.0607 ns | 0.0568 ns |  3.72 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 38.099 ns | 0.0424 ns | 0.0397 ns |  4.94 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |
