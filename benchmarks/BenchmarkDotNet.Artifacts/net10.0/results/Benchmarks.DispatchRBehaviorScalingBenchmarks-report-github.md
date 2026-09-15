```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.579 ns | 0.0277 ns | 0.0406 ns |  5.586 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 33.532 ns | 0.2286 ns | 0.3422 ns | 33.437 ns |  6.01 |    0.07 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 34.850 ns | 0.1705 ns | 0.2499 ns | 34.856 ns |  6.25 |    0.06 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 36.921 ns | 0.1641 ns | 0.2405 ns | 36.858 ns |  6.62 |    0.06 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 34.897 ns | 0.2100 ns | 0.3144 ns | 34.760 ns |  6.26 |    0.07 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 41.219 ns | 0.3598 ns | 0.5385 ns | 41.246 ns |  7.39 |    0.11 |    6 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 38.434 ns | 0.2186 ns | 0.3204 ns | 38.325 ns |  6.89 |    0.08 |    5 | 0.0055 |      72 B |        1.00 |
