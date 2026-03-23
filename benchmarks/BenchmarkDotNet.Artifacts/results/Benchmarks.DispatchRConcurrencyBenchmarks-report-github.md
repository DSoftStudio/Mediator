```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method               | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,322.88 ns | 3.737 ns | 3.312 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,721.47 ns | 7.646 ns | 7.152 ns |  2.81 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    85.49 ns | 0.821 ns | 0.768 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,031.98 ns | 5.230 ns | 4.893 ns | 35.47 |    0.31 |    2 | 0.0038 |      - |      72 B |        1.00 |
