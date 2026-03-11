```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method         | Mean     | Error    | StdDev   | Rank | Gen0   | Allocated |
|--------------- |---------:|---------:|---------:|-----:|-------:|----------:|
| DispatchR_Send | 34.46 ns | 0.092 ns | 0.076 ns |    1 | 0.0055 |      72 B |
