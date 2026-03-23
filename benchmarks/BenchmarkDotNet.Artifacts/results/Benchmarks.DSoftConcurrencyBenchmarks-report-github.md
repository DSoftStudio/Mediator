```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method            | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut     | FanOut     | 1,310.48 ns | 11.886 ns | 11.118 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut      | FanOut     | 1,368.67 ns | 26.464 ns | 25.991 ns |  1.04 |    0.02 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    92.01 ns |  0.266 ns |  0.249 ns |  1.00 |    0.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   192.87 ns |  0.473 ns |  0.395 ns |  2.10 |    0.01 |    2 | 0.0055 |      - |      72 B |        1.00 |
