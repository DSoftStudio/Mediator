```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.31 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method             | Categories | Mean        | Error     | StdDev    | Median      | Ratio  | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |------------:|----------:|----------:|------------:|-------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,338.54 ns |  4.723 ns |  7.070 ns | 1,340.10 ns |   1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatR_FanOut     | FanOut     | 4,600.43 ns | 11.059 ns | 16.552 ns | 4,598.66 ns |   3.44 |    0.02 |    2 | 1.5640 | 0.0381 |   20536 B |        2.41 |
|                    |            |             |           |           |             |        |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |    35.80 ns |  0.711 ns |  1.065 ns |    35.20 ns |   1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatR_Throughput | Throughput | 3,910.66 ns |  7.277 ns | 10.666 ns | 3,911.42 ns | 109.34 |    3.13 |    2 | 1.8921 |      - |   24800 B |          NA |
