```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,306.63 ns |  5.004 ns |  4.436 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 4,495.12 ns | 42.499 ns | 39.754 ns |  3.44 |    0.03 |    2 | 0.6485 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    90.17 ns |  0.585 ns |  0.547 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,025.40 ns | 10.126 ns |  9.472 ns | 33.55 |    0.22 |    2 | 0.0038 |      - |      72 B |        1.00 |
