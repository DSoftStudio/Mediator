```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Send     |   6.792 ns | 0.0391 ns | 0.0366 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send      |  15.536 ns | 0.0328 ns | 0.0307 ns |  2.29 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send |  21.227 ns | 0.0613 ns | 0.0574 ns |  3.13 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send  |  53.481 ns | 0.0818 ns | 0.0725 ns |  7.87 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |
| MediatR_Send    | 150.202 ns | 0.2011 ns | 0.1783 ns | 22.12 |    0.12 |    5 | 0.0832 |    1088 B |       15.11 |
