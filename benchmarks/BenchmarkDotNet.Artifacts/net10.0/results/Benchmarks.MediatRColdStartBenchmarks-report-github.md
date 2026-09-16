```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                            | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| MediatR_Startup_ContainerOnly     | 34.45 ms | 5.315 ms | 9.447 ms | 32.94 ms |  1.03 |    0.30 |    1 |  12.41 KB |        1.00 |
| MediatR_Startup_WithFirstDispatch | 34.58 ms | 0.204 ms | 0.363 ms | 34.62 ms |  1.03 |    0.11 |    2 |  15.22 KB |        1.23 |
