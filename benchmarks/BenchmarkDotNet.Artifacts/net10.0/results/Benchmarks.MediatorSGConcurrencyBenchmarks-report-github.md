```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,401.31 ns | 10.284 ns |  9.117 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 2,142.56 ns | 20.806 ns | 18.444 ns |  1.53 |    0.02 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                       |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    92.47 ns |  0.994 ns |  0.929 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput | 1,169.90 ns |  6.476 ns |  6.058 ns | 12.65 |    0.14 |    2 | 0.0038 |      - |      72 B |        1.00 |
