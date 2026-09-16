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
| Method                     | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|--------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| DSoft_Startup_DiFloor      | 11.67 ms | 0.121 ms | 0.215 ms | 11.64 ms |  1.00 |    0.00 | baseline |    1 |   7.42 KB |        1.00 |
| DSoft_Startup_Registered   | 30.51 ms | 0.230 ms | 0.408 ms | 30.42 ms |  2.61 |    0.06 | 18.78 ms |    2 |   28.4 KB |        3.83 |
| DSoft_Startup_FirstRequest | 34.77 ms | 0.650 ms | 1.156 ms | 34.37 ms |  2.98 |    0.11 | 22.73 ms |    3 |  32.48 KB |        4.38 |
