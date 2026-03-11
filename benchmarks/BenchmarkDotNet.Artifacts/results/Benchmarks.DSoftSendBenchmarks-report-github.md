```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall            |  5.746 ns | 0.0370 ns | 0.0328 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send            | 12.091 ns | 0.0355 ns | 0.0315 ns |  2.10 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 34.633 ns | 0.0897 ns | 0.0839 ns |  6.03 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 39.381 ns | 0.0665 ns | 0.0622 ns |  6.85 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |
