```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,308.91 ns |  5.645 ns |  5.004 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut         | FanOut     | 2,004.99 ns |  6.939 ns |  6.151 ns |  1.53 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,705.80 ns |  5.276 ns |  4.936 ns |  2.83 |    0.01 |    3 | 0.6523 | 0.0153 |    8536 B |        1.00 |
| MediatR_FanOut       | FanOut     | 4,566.08 ns | 13.949 ns | 13.048 ns |  3.49 |    0.02 |    4 | 1.6251 | 0.0381 |   21336 B |        2.50 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    89.86 ns |  0.404 ns |  0.358 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput     | Throughput |   718.02 ns |  0.910 ns |  0.851 ns |  7.99 |    0.03 |    2 | 0.0048 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,030.77 ns |  3.078 ns |  2.879 ns | 33.73 |    0.13 |    3 | 0.0038 |      - |      72 B |        1.00 |
| MediatR_Throughput   | Throughput | 3,603.61 ns | 11.144 ns | 10.424 ns | 40.10 |    0.19 |    4 | 1.5335 |      - |   20072 B |      278.78 |
