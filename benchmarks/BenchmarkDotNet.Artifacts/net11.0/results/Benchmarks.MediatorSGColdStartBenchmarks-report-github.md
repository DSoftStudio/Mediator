```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 7.16 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                          | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| MediatorSG_Startup_DiFloor      | 11.63 ms | 0.107 ms | 0.191 ms | 11.65 ms |  1.00 |    0.00 | baseline |    1 |   7.42 KB |        1.00 |
| MediatorSG_Startup_Registered   | 14.74 ms | 0.123 ms | 0.219 ms | 14.74 ms |  1.27 |    0.03 | 3.09 ms  |    2 |   22.2 KB |        2.99 |
| MediatorSG_Startup_FirstRequest | 21.90 ms | 0.221 ms | 0.394 ms | 21.81 ms |  1.88 |    0.05 | 10.15 ms |    3 | 153.76 KB |       20.72 |
