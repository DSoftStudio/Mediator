```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method               | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,256.71 ns | 2.806 ns | 4.200 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 4,158.06 ns | 6.061 ns | 8.693 ns |  3.31 |    0.01 |    2 | 0.6485 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    90.42 ns | 0.170 ns | 0.244 ns |  1.00 |    0.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,031.05 ns | 4.604 ns | 6.603 ns | 33.52 |    0.11 |    2 | 0.0038 |      - |      72 B |        1.00 |
