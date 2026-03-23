```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,317.43 ns |  6.104 ns |  5.710 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,725.83 ns | 11.132 ns | 10.413 ns |  1.31 |    0.01 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                       |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    94.30 ns |  0.499 ns |  0.416 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput |   849.36 ns |  1.284 ns |  1.201 ns |  9.01 |    0.04 |    2 | 0.0048 |      - |      72 B |        1.00 |
