```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method     | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall | 5.646 ns | 0.0329 ns | 0.0292 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 6.276 ns | 0.1451 ns | 0.2035 ns |  1.11 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
