```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.34 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method             | Categories | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |------------:|----------:|----------:|-------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,251.65 ns |  3.279 ns |  2.907 ns |   1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatR_FanOut     | FanOut     | 4,511.74 ns | 20.858 ns | 17.418 ns |   3.60 |    0.02 |    2 | 1.5640 | 0.0381 |   20536 B |        2.41 |
|                    |            |             |           |           |        |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |    34.60 ns |  0.042 ns |  0.037 ns |   1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatR_Throughput | Throughput | 4,011.28 ns | 18.817 ns | 16.681 ns | 115.92 |    0.48 |    2 | 1.8921 |      - |   24800 B |          NA |
