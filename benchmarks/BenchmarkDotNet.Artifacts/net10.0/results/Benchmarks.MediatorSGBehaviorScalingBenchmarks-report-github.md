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
| DirectCall      |  5.542 ns | 0.0311 ns | 0.0465 ns |  5.558 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 15.027 ns | 0.0273 ns | 0.0373 ns | 15.023 ns |  2.71 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 15.679 ns | 0.0251 ns | 0.0344 ns | 15.683 ns |  2.83 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 18.376 ns | 0.0422 ns | 0.0631 ns | 18.376 ns |  3.32 |    0.03 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 20.602 ns | 0.0343 ns | 0.0514 ns | 20.591 ns |  3.72 |    0.03 |    5 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 29.472 ns | 0.0586 ns | 0.0876 ns | 29.456 ns |  5.32 |    0.05 |    6 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 36.694 ns | 0.0523 ns | 0.0783 ns | 36.676 ns |  6.62 |    0.06 |    7 | 0.0055 |      72 B |        1.00 |
