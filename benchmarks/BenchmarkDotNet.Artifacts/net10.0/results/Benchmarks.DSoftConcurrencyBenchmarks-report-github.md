```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method            | Categories | Mean        | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,274.02 ns | 2.809 ns | 4.205 ns |  0.98 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,296.28 ns | 3.170 ns | 4.745 ns |  1.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |      |        |        |           |             |
| Direct_Throughput | Throughput |    92.99 ns | 0.230 ns | 0.344 ns |  1.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   108.82 ns | 0.301 ns | 0.432 ns |  1.17 |    2 | 0.0055 |      - |      72 B |        1.00 |
