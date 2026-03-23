```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.745 ns | 0.0422 ns | 0.0394 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 13.820 ns | 0.0389 ns | 0.0364 ns |  2.05 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 15.635 ns | 0.0178 ns | 0.0158 ns |  2.32 |    3 | 0.0055 |      72 B |        1.00 |
