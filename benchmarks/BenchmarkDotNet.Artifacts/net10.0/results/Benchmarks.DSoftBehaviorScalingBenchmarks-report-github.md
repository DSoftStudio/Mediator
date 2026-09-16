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
| DirectCall      |  5.953 ns | 0.0335 ns | 0.0502 ns |  5.963 ns |  1.00 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors |  5.637 ns | 0.0211 ns | 0.0316 ns |  5.639 ns |  0.95 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 11.493 ns | 0.0396 ns | 0.0580 ns | 11.506 ns |  1.93 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 12.124 ns | 0.0215 ns | 0.0315 ns | 12.119 ns |  2.04 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 12.027 ns | 0.0334 ns | 0.0479 ns | 12.031 ns |  2.02 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 11.474 ns | 0.0756 ns | 0.1132 ns | 11.441 ns |  1.93 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 12.048 ns | 0.0202 ns | 0.0295 ns | 12.040 ns |  2.02 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
