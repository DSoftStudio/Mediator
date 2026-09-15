```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method             | Categories | Mean       | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |-----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,254.9 ns | 11.55 ns | 10.81 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |   8.34 KB |        1.00 |
| MediatR_FanOut     | FanOut     | 4,638.8 ns | 38.90 ns | 36.38 ns |  3.70 |    0.04 |    2 | 1.6251 | 0.0381 |  20.84 KB |        2.50 |
|                    |            |            |          |          |       |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |   392.4 ns |  1.65 ns |  1.55 ns |  1.00 |    0.01 |    1 | 0.5560 |      - |    7.1 KB |        1.00 |
| MediatR_Throughput | Throughput | 3,944.0 ns | 23.38 ns | 21.87 ns | 10.05 |    0.07 |    2 | 1.5335 |      - |   19.6 KB |        2.76 |
