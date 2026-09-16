```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 7.64 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                          | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DSoft_Startup_ContainerOnly     | 15.44 ms | 5.494 ms | 9.766 ms | 13.93 ms |  1.00 |    0.00 |    1 |  17.25 KB |        1.00 |
| DSoft_Startup_WithFirstDispatch | 17.28 ms | 0.131 ms | 0.233 ms | 17.29 ms |  1.22 |    0.16 |    2 |  17.25 KB |        1.00 |
