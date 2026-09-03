```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.32 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method               | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,296.16 ns | 5.089 ns | 4.761 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,711.58 ns | 6.922 ns | 5.780 ns |  2.86 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    35.40 ns | 0.046 ns | 0.040 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DispatchR_Throughput | Throughput | 2,557.63 ns | 5.798 ns | 5.423 ns | 72.25 |    0.17 |    2 |      - |      - |         - |          NA |
