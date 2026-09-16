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
| DirectCall      |  5.433 ns | 0.0196 ns | 0.0287 ns |  5.433 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 33.410 ns | 0.0709 ns | 0.1060 ns | 33.408 ns |  6.15 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 35.141 ns | 0.0941 ns | 0.1408 ns | 35.105 ns |  6.47 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 34.384 ns | 0.0607 ns | 0.0908 ns | 34.367 ns |  6.33 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 35.281 ns | 0.0684 ns | 0.1002 ns | 35.245 ns |  6.49 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 34.479 ns | 0.0602 ns | 0.0901 ns | 34.468 ns |  6.35 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 43.749 ns | 0.0634 ns | 0.0930 ns | 43.736 ns |  8.05 |    0.05 |    4 | 0.0055 |      72 B |        1.00 |
