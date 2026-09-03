```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,302.92 ns | 3.293 ns | 2.749 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,603.82 ns | 5.457 ns | 4.838 ns |  1.23 |    0.00 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    93.61 ns | 0.437 ns | 0.409 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput |   892.03 ns | 0.689 ns | 0.611 ns |  9.53 |    0.04 |    2 | 0.0048 |      - |      72 B |        1.00 |
