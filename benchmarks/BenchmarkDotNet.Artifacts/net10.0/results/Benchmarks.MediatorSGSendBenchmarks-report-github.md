```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                     | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  5.539 ns | 0.0246 ns | 0.0369 ns |  5.545 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 22.034 ns | 0.1044 ns | 0.1497 ns | 21.959 ns |  3.98 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 29.607 ns | 0.1024 ns | 0.1469 ns | 29.584 ns |  5.35 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
