```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.32 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,306.52 ns | 6.177 ns | 5.778 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,618.57 ns | 4.189 ns | 3.919 ns |  1.24 |    0.01 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    36.03 ns | 0.719 ns | 1.259 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatorSG_Throughput | Throughput |   408.69 ns | 0.810 ns | 0.757 ns | 11.36 |    0.43 |    2 |      - |      - |         - |          NA |
