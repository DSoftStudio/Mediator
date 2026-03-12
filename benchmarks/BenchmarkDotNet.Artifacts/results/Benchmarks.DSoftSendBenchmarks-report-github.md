```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.646 ns | 0.0289 ns | 0.0270 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send            |  7.886 ns | 0.1765 ns | 0.1651 ns |  1.19 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 15.306 ns | 0.2072 ns | 0.1938 ns |  2.30 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 16.544 ns | 0.2509 ns | 0.2347 ns |  2.49 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |
