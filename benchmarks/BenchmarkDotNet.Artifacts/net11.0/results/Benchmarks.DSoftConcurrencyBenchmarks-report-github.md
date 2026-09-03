```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.26 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method            | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,287.13 ns | 2.821 ns | 2.501 ns |  0.99 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,295.62 ns | 8.462 ns | 7.915 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    35.79 ns | 0.682 ns | 0.638 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DSoft_Throughput  | Throughput |    45.49 ns | 0.078 ns | 0.073 ns |  1.27 |    0.02 |    2 |      - |      - |         - |          NA |
