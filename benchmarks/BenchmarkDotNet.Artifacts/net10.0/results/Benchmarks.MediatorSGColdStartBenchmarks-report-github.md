```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                          | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| MediatorSG_Startup_DiFloor      | 13.74 ms | 0.116 ms | 0.206 ms | 13.73 ms |  1.00 |    0.02 | baseline |    1 |   7.45 KB |        1.00 |
| MediatorSG_Startup_Registered   | 16.63 ms | 0.100 ms | 0.177 ms | 16.61 ms |  1.21 |    0.02 | 2.88 ms  |    2 |   34.3 KB |        4.60 |
| MediatorSG_Startup_FirstRequest | 25.53 ms | 0.186 ms | 0.330 ms | 25.50 ms |  1.86 |    0.04 | 11.77 ms |    3 | 157.09 KB |       21.08 |
