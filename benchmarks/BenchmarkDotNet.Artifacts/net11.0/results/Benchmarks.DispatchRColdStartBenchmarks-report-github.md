```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.39 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                              | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DispatchR_Startup_ContainerOnly     | 14.40 ms | 5.518 ms | 9.808 ms | 12.81 ms |  1.00 |    0.00 |    1 |  14.63 KB |        1.00 |
| DispatchR_Startup_WithFirstDispatch | 15.01 ms | 0.179 ms | 0.317 ms | 14.89 ms |  1.14 |    0.16 |    2 |  16.91 KB |        1.16 |
