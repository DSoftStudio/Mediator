```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                              | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DispatchR_Startup_ContainerOnly     | 15.46 ms | 5.314 ms | 9.445 ms | 13.93 ms |  1.08 |    0.67 |    1 |  14.66 KB |        1.00 |
| DispatchR_Startup_WithFirstDispatch | 18.07 ms | 0.098 ms | 0.174 ms | 18.02 ms |  1.27 |    0.16 |    2 |  17.04 KB |        1.16 |
