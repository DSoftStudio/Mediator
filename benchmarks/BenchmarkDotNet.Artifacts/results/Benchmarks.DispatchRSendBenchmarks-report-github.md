```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                    | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DispatchR_Send            | 35.21 ns | 0.140 ns | 0.124 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 52.92 ns | 0.077 ns | 0.072 ns |  1.50 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 53.50 ns | 0.165 ns | 0.155 ns |  1.52 |    2 | 0.0055 |      72 B |        1.00 |
