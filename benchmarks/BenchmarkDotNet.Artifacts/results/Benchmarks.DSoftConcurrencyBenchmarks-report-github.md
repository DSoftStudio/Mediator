```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method            | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut     | FanOut     | 1,270.57 ns | 8.521 ns | 7.971 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut      | FanOut     | 1,351.01 ns | 5.509 ns | 5.153 ns |  1.06 |    0.01 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    85.24 ns | 0.555 ns | 0.519 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   194.93 ns | 0.749 ns | 0.701 ns |  2.29 |    0.02 |    2 | 0.0055 |      - |      72 B |        1.00 |
