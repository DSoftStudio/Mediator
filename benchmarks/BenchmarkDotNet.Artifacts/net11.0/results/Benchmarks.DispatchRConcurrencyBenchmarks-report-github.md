```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.44 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,304.42 ns | 11.290 ns | 16.548 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,693.85 ns |  6.257 ns |  8.974 ns |  2.83 |    0.04 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    34.66 ns |  0.089 ns |  0.127 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DispatchR_Throughput | Throughput | 2,559.92 ns |  9.243 ns | 13.835 ns | 73.86 |    0.47 |    2 |      - |      - |         - |          NA |
