```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  5.709 ns | 0.0214 ns | 0.0320 ns |  5.710 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 34.498 ns | 0.0642 ns | 0.0941 ns | 34.504 ns |  6.04 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 34.567 ns | 0.0510 ns | 0.0748 ns | 34.560 ns |  6.06 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
