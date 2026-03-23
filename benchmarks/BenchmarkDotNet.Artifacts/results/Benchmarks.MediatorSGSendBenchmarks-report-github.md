```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  7.058 ns | 0.0393 ns | 0.0368 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 27.965 ns | 0.0437 ns | 0.0408 ns |  3.96 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 36.753 ns | 0.0843 ns | 0.0789 ns |  5.21 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |
