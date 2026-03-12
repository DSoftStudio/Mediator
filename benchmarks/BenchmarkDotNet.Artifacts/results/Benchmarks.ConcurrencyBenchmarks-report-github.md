```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method                | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,441.58 ns | 18.340 ns | 17.155 ns |  1.00 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut          | FanOut     | 1,476.96 ns | 27.747 ns | 25.954 ns |  1.02 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,697.04 ns | 24.826 ns | 23.222 ns |  1.18 |    0.02 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut      | FanOut     | 3,805.10 ns | 32.608 ns | 30.501 ns |  2.64 |    0.04 |    3 | 0.6523 | 0.0153 |    8536 B |        1.00 |
| MediatR_FanOut        | FanOut     | 4,812.04 ns | 38.008 ns | 35.553 ns |  3.34 |    0.05 |    4 | 1.6251 | 0.0381 |   21336 B |        2.50 |
|                       |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    85.73 ns |  0.460 ns |  0.408 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput      | Throughput |   193.64 ns |  0.694 ns |  0.649 ns |  2.26 |    0.01 |    2 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput |   868.61 ns |  0.988 ns |  0.924 ns | 10.13 |    0.05 |    3 | 0.0048 |      - |      72 B |        1.00 |
| DispatchR_Throughput  | Throughput | 3,010.34 ns |  3.954 ns |  3.698 ns | 35.12 |    0.17 |    4 | 0.0038 |      - |      72 B |        1.00 |
| MediatR_Throughput    | Throughput | 3,644.80 ns | 11.132 ns | 10.413 ns | 42.52 |    0.23 |    5 | 1.5335 |      - |   20072 B |      278.78 |
