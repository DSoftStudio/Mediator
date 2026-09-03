```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,301.79 ns |  4.147 ns |  3.879 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,711.34 ns | 15.207 ns | 14.225 ns |  2.85 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    91.67 ns |  0.339 ns |  0.265 ns |  1.00 |    0.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,012.57 ns |  4.133 ns |  3.866 ns | 32.86 |    0.10 |    2 | 0.0038 |      - |      72 B |        1.00 |
