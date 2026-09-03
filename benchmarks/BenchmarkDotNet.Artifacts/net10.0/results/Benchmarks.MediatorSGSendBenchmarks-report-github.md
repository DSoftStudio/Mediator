```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  5.495 ns | 0.0293 ns | 0.0245 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 27.613 ns | 0.1095 ns | 0.0971 ns |  5.03 |    0.03 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 36.567 ns | 0.0471 ns | 0.0368 ns |  6.65 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |
