```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.241 ns | 0.0548 ns | 0.0820 ns |  6.218 ns |  1.00 |    0.02 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 11.966 ns | 0.2559 ns | 0.3503 ns | 11.810 ns |  1.92 |    0.06 |    3 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 11.490 ns | 0.0247 ns | 0.0362 ns | 11.484 ns |  1.84 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
