```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                         | Mean     | Error     | StdDev    | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|------------------------------- |---------:|----------:|----------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| DispatchR_Startup_DiFloor      | 17.23 ms | 11.112 ms | 19.751 ms | 14.07 ms |  1.19 |    1.38 | baseline |    1 |   7.45 KB |        1.00 |
| DispatchR_Startup_Registered   | 26.96 ms |  9.499 ms | 16.884 ms | 24.25 ms |  1.87 |    1.20 | 10.18 ms |    2 | 746.65 KB |      100.18 |
| DispatchR_Startup_FirstRequest | 27.84 ms |  0.253 ms |  0.450 ms | 27.83 ms |  1.93 |    0.28 | 13.76 ms |    3 | 748.87 KB |      100.48 |
