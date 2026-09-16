```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                               | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| MediatorSG_Startup_ContainerOnly     | 16.02 ms | 5.325 ms | 9.465 ms | 14.52 ms |  1.08 |    0.65 |    1 |  15.51 KB |        1.00 |
| MediatorSG_Startup_WithFirstDispatch | 23.30 ms | 0.196 ms | 0.349 ms | 23.34 ms |  1.57 |    0.20 |    2 | 150.35 KB |        9.70 |
