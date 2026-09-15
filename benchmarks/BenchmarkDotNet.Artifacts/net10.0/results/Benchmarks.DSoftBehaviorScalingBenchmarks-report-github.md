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
| DirectCall      |  5.813 ns | 0.0209 ns | 0.0312 ns |  5.816 ns |  1.00 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors |  5.606 ns | 0.0223 ns | 0.0312 ns |  5.603 ns |  0.96 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 12.055 ns | 0.0220 ns | 0.0330 ns | 12.057 ns |  2.07 |    0.01 |    4 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 17.717 ns | 0.0209 ns | 0.0293 ns | 17.712 ns |  3.05 |    0.02 |    5 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 11.367 ns | 0.0254 ns | 0.0365 ns | 11.355 ns |  1.96 |    0.01 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 11.334 ns | 0.0191 ns | 0.0248 ns | 11.342 ns |  1.95 |    0.01 |    3 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 12.027 ns | 0.0239 ns | 0.0335 ns | 12.019 ns |  2.07 |    0.01 |    4 | 0.0055 |      72 B |        1.00 |
