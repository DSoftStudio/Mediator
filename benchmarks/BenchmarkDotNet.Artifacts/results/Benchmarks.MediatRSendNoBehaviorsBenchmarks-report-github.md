```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   | 10.20 ns | 0.054 ns | 0.051 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send | 41.33 ns | 0.080 ns | 0.071 ns |  4.05 |    0.02 |    2 | 0.0208 |     272 B |        1.89 |
