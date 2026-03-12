```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method             | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish     |   3.712 ns | 0.0467 ns | 0.0436 ns |  1.00 |    0.02 |    1 |      - |         - |          NA |
| DSoft_Publish      |   8.526 ns | 0.0222 ns | 0.0208 ns |  2.30 |    0.03 |    2 |      - |         - |          NA |
| MediatorSG_Publish |  10.170 ns | 0.0167 ns | 0.0148 ns |  2.74 |    0.03 |    3 |      - |         - |          NA |
| DispatchR_Publish  |  34.967 ns | 0.1144 ns | 0.1070 ns |  9.42 |    0.11 |    4 |      - |         - |          NA |
| MediatR_Publish    | 136.145 ns | 0.4880 ns | 0.4326 ns | 36.68 |    0.44 |    5 | 0.0587 |     768 B |          NA |
