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
| Method                       | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated  | Alloc Ratio |
|----------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|-----------:|------------:|
| MediatR_Startup_DiFloor      | 11.65 ms | 0.114 ms | 0.203 ms | 11.63 ms |  1.00 |    0.00 | baseline |    1 |    7.42 KB |        1.00 |
| MediatR_Startup_Registered   | 42.77 ms | 3.811 ms | 6.773 ms | 41.69 ms |  3.67 |    0.58 | 30.05 ms |    2 | 1624.42 KB |      218.87 |
| MediatR_Startup_FirstRequest | 43.20 ms | 0.208 ms | 0.371 ms | 43.14 ms |  3.71 |    0.07 | 31.51 ms |    3 | 1680.12 KB |      226.37 |
