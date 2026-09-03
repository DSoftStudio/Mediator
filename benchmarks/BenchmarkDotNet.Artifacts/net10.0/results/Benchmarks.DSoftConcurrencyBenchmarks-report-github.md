```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method            | Categories | Mean        | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,293.35 ns | 3.446 ns | 3.224 ns |  0.99 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,309.54 ns | 3.664 ns | 3.248 ns |  1.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |      |        |        |           |             |
| Direct_Throughput | Throughput |    92.85 ns | 0.201 ns | 0.179 ns |  1.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   116.95 ns | 0.351 ns | 0.328 ns |  1.26 |    2 | 0.0055 |      - |      72 B |        1.00 |
