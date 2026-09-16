```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-LKYEZY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=1  LaunchCount=40  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                          | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DSoft_Startup_ContainerOnly     | 17.00 ms | 5.541 ms | 9.849 ms | 15.40 ms |  1.08 |    0.64 |    1 |   17.3 KB |        1.00 |
| DSoft_Startup_WithFirstDispatch | 19.92 ms | 0.135 ms | 0.240 ms | 19.92 ms |  1.26 |    0.16 |    2 |  17.37 KB |        1.00 |
