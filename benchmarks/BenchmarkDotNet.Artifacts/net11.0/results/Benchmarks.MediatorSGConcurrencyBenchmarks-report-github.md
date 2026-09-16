```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.36 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,318.31 ns | 3.763 ns | 5.516 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 2,028.31 ns | 3.160 ns | 4.730 ns |  1.54 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    36.22 ns | 0.774 ns | 1.135 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatorSG_Throughput | Throughput |   782.16 ns | 6.181 ns | 9.251 ns | 21.62 |    0.76 |    2 |      - |      - |         - |          NA |
