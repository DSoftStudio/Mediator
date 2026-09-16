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
| DirectCall                 |  5.708 ns | 0.0171 ns | 0.0246 ns |  5.709 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 22.350 ns | 0.0494 ns | 0.0740 ns | 22.328 ns |  3.92 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 28.931 ns | 0.0408 ns | 0.0545 ns | 28.929 ns |  5.07 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
