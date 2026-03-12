```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_ColdStart      | 1.630 μs | 0.0105 μs | 0.0099 μs |  1.00 |    0.01 |    1 | 0.7229 | 0.0401 |   9.23 KB |        1.00 |
| DispatchR_ColdStart  | 1.905 μs | 0.0318 μs | 0.0282 μs |  1.17 |    0.02 |    2 | 0.6847 | 0.0191 |   8.76 KB |        0.95 |
| MediatR_ColdStart    | 3.097 μs | 0.0469 μs | 0.0416 μs |  1.90 |    0.03 |    3 | 0.9766 | 0.0343 |   12.5 KB |        1.35 |
| MediatorSG_ColdStart | 7.414 μs | 0.0327 μs | 0.0306 μs |  4.55 |    0.03 |    4 | 2.1667 | 0.1526 |   27.8 KB |        3.01 |
