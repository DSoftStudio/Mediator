```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                       | Mean     | Error     | StdDev    | Median   | Ratio | RatioSD | Added    | Rank | Allocated  | Alloc Ratio |
|----------------------------- |---------:|----------:|----------:|---------:|------:|--------:|--------- |-----:|-----------:|------------:|
| MediatR_Startup_DiFloor      | 13.70 ms |  0.069 ms |  0.123 ms | 13.71 ms |  1.00 |    0.01 | baseline |    1 |    7.45 KB |        1.00 |
| MediatR_Startup_FirstRequest | 52.30 ms |  0.264 ms |  0.469 ms | 52.36 ms |  3.82 |    0.05 | 38.65 ms |    2 | 2203.68 KB |      295.67 |
| MediatR_Startup_Registered   | 52.40 ms | 10.010 ms | 17.793 ms | 49.54 ms |  3.83 |    1.28 | 35.83 ms |    3 | 2201.04 KB |      295.32 |
