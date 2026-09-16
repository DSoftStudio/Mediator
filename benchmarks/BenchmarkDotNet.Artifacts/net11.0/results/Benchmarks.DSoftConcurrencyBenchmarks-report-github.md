```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 7.65 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method            | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,329.28 ns | 3.984 ns | 5.963 ns |  0.97 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,374.96 ns | 4.174 ns | 6.119 ns |  1.00 |    0.00 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    36.49 ns | 0.778 ns | 1.165 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DSoft_Throughput  | Throughput |    45.72 ns | 0.095 ns | 0.140 ns |  1.25 |    0.04 |    2 |      - |      - |         - |          NA |
