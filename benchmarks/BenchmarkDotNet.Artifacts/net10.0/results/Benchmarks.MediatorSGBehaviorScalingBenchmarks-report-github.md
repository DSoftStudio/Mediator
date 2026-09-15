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
| DirectCall      |  5.779 ns | 0.2290 ns | 0.3427 ns |  5.704 ns |  1.00 |    0.08 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 15.087 ns | 0.0299 ns | 0.0438 ns | 15.071 ns |  2.62 |    0.15 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 15.829 ns | 0.0284 ns | 0.0416 ns | 15.831 ns |  2.75 |    0.16 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 19.500 ns | 0.0642 ns | 0.0857 ns | 19.481 ns |  3.39 |    0.19 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 22.241 ns | 0.1400 ns | 0.2095 ns | 22.264 ns |  3.86 |    0.22 |    5 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 28.972 ns | 0.2352 ns | 0.3521 ns | 29.011 ns |  5.03 |    0.29 |    6 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 36.088 ns | 0.1914 ns | 0.2806 ns | 36.019 ns |  6.27 |    0.36 |    7 | 0.0055 |      72 B |        1.00 |
