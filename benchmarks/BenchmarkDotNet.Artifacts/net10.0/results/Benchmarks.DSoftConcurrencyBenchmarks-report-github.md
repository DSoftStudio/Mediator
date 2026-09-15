```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method            | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,377.70 ns | 18.171 ns | 16.997 ns |  0.99 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,397.93 ns | 22.980 ns | 21.495 ns |  1.00 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    85.81 ns |  0.474 ns |  0.420 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   116.88 ns |  0.437 ns |  0.388 ns |  1.36 |    0.01 |    2 | 0.0055 |      - |      72 B |        1.00 |
