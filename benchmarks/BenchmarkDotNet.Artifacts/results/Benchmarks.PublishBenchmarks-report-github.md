```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method            | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish    |   3.486 ns | 0.0199 ns | 0.0177 ns |  1.00 |    0.01 |    1 |      - |         - |          NA |
| DSoft_Publish     |  18.443 ns | 0.0374 ns | 0.0350 ns |  5.29 |    0.03 |    2 |      - |         - |          NA |
| DispatchR_Publish |  35.225 ns | 0.0563 ns | 0.0500 ns | 10.10 |    0.05 |    3 |      - |         - |          NA |
| MediatR_Publish   | 124.263 ns | 0.4466 ns | 0.4177 ns | 35.64 |    0.21 |    4 | 0.0587 |     768 B |          NA |
