```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  7.124 ns | 0.0539 ns | 0.0504 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send            | 34.653 ns | 0.1325 ns | 0.1175 ns |  4.86 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 52.891 ns | 0.0945 ns | 0.0884 ns |  7.42 |    0.05 |    3 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 55.455 ns | 0.0528 ns | 0.0412 ns |  7.78 |    0.05 |    4 | 0.0055 |      72 B |        1.00 |
