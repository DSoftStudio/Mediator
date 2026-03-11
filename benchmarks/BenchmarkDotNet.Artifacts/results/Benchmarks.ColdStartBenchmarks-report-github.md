```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method              | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_ColdStart     | 1.621 μs | 0.0168 μs | 0.0140 μs |  1.00 |    0.01 |    1 | 0.7172 | 0.0153 |   9.23 KB |        1.00 |
| DispatchR_ColdStart | 1.799 μs | 0.0139 μs | 0.0130 μs |  1.11 |    0.01 |    2 | 0.6714 | 0.0153 |   8.76 KB |        0.95 |
| MediatR_ColdStart   | 3.114 μs | 0.0128 μs | 0.0120 μs |  1.92 |    0.02 |    3 | 0.9766 | 0.0343 |   12.5 KB |        1.35 |
