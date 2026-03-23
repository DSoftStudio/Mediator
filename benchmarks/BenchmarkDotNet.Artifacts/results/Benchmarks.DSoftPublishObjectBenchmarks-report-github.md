```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 4.568 ns | 0.0289 ns | 0.0270 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish_Object  | 6.561 ns | 0.0425 ns | 0.0397 ns |  1.44 |    2 |         - |          NA |
