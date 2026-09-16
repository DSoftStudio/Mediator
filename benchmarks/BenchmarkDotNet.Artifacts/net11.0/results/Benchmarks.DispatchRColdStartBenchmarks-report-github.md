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
| Method                         | Mean     | Error    | StdDev    | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|----------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| DispatchR_Startup_DiFloor      | 13.51 ms | 5.522 ms |  9.816 ms | 11.95 ms |  1.00 |    0.00 | baseline |    1 |  19.47 KB |        1.00 |
| DispatchR_Startup_FirstRequest | 24.14 ms | 0.209 ms |  0.372 ms | 24.08 ms |  1.98 |    0.27 | 12.12 ms |    2 | 650.93 KB |       33.43 |
| DispatchR_Startup_Registered   | 24.48 ms | 9.630 ms | 17.117 ms | 21.75 ms |  2.01 |    1.42 | 9.79 ms  |    3 | 648.85 KB |       33.33 |
