```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                     | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|--------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| DSoft_Startup_DiFloor      | 13.80 ms | 0.102 ms | 0.181 ms | 13.80 ms |  1.00 |    0.02 | baseline |    1 |   7.45 KB |        1.00 |
| DSoft_Startup_Registered   | 35.51 ms | 0.183 ms | 0.325 ms | 35.44 ms |  2.57 |    0.04 | 21.64 ms |    2 |  30.21 KB |        4.05 |
| DSoft_Startup_FirstRequest | 40.01 ms | 0.238 ms | 0.424 ms | 39.93 ms |  2.90 |    0.05 | 26.13 ms |    3 |  33.91 KB |        4.55 |
