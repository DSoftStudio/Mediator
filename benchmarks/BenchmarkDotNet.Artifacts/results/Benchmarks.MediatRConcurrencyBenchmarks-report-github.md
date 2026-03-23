```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method             | Categories | Mean       | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |-----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,279.2 ns |  6.02 ns |  5.63 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |   8.34 KB |        1.00 |
| MediatR_FanOut     | FanOut     | 4,531.2 ns | 12.79 ns | 10.68 ns |  3.54 |    0.02 |    2 | 1.6251 | 0.0381 |  20.84 KB |        2.50 |
|                    |            |            |          |          |       |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |   381.7 ns |  3.01 ns |  2.67 ns |  1.00 |    0.01 |    1 | 0.5560 |      - |    7.1 KB |        1.00 |
| MediatR_Throughput | Throughput | 3,629.7 ns | 11.80 ns | 11.04 ns |  9.51 |    0.07 |    2 | 1.5335 |      - |   19.6 KB |        2.76 |
