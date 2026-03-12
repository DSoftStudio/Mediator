```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method         | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish | 3.701 ns | 0.0504 ns | 0.0446 ns |  1.00 |    0.02 |    1 |         - |          NA |
| DSoft_Publish  | 8.034 ns | 0.0180 ns | 0.0159 ns |  2.17 |    0.03 |    2 |         - |          NA |
