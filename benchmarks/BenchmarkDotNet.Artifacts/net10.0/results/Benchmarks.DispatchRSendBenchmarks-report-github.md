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
| DirectCall                |  5.824 ns | 0.0296 ns | 0.0434 ns |  5.815 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 34.972 ns | 0.1617 ns | 0.2319 ns | 34.911 ns |  6.01 |    0.06 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 42.786 ns | 2.2194 ns | 3.3219 ns | 43.975 ns |  7.35 |    0.56 |    3 | 0.0055 |      72 B |        1.00 |
