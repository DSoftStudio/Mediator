```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                  | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| MediatR_Send            |  44.52 ns | 0.901 ns | 0.964 ns |  1.00 |    0.03 |    1 | 0.0208 |     272 B |        1.00 |
| MediatR_Send_3Behaviors | 107.88 ns | 0.571 ns | 0.534 ns |  2.42 |    0.05 |    2 | 0.0612 |     800 B |        2.94 |
| MediatR_Send_5Behaviors | 153.65 ns | 1.315 ns | 1.230 ns |  3.45 |    0.08 |    3 | 0.0832 |    1088 B |        4.00 |
