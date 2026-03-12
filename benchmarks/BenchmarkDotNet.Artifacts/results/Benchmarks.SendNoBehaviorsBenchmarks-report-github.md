```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Send     |  6.493 ns | 0.0234 ns | 0.0208 ns |  1.00 |    0.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send      |  7.077 ns | 0.0518 ns | 0.0484 ns |  1.09 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 12.538 ns | 0.0350 ns | 0.0328 ns |  1.93 |    0.01 |    3 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send  | 33.374 ns | 0.1147 ns | 0.1017 ns |  5.14 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
| MediatR_Send    | 42.060 ns | 0.1033 ns | 0.0916 ns |  6.48 |    0.02 |    5 | 0.0208 |     272 B |        3.78 |
