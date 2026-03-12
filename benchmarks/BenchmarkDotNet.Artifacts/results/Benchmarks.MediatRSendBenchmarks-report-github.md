```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                  | Mean      | Error    | StdDev   | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |  10.21 ns | 0.234 ns | 0.468 ns |   9.997 ns |  1.00 |    0.06 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send            |  41.89 ns | 0.628 ns | 0.556 ns |  41.647 ns |  4.11 |    0.19 |    2 | 0.0208 |     272 B |        1.89 |
| MediatR_Send_3Behaviors | 111.24 ns | 0.318 ns | 0.265 ns | 111.301 ns | 10.91 |    0.47 |    3 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 161.50 ns | 3.163 ns | 2.959 ns | 161.061 ns | 15.84 |    0.74 |    4 | 0.0832 |    1088 B |        7.56 |
