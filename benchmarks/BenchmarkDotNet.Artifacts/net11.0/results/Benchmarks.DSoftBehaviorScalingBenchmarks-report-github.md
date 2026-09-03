```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.41 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method          | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      | 1.901 ns | 0.0066 ns | 0.0062 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 2.371 ns | 0.0101 ns | 0.0094 ns |  1.25 |    0.01 |    2 |         - |          NA |
| Send_1Behaviors | 4.366 ns | 0.0063 ns | 0.0056 ns |  2.30 |    0.01 |    3 |         - |          NA |
| Send_2Behaviors | 4.857 ns | 0.0073 ns | 0.0068 ns |  2.56 |    0.01 |    4 |         - |          NA |
| Send_3Behaviors | 5.390 ns | 0.0173 ns | 0.0153 ns |  2.84 |    0.01 |    5 |         - |          NA |
| Send_5Behaviors | 6.405 ns | 0.0091 ns | 0.0085 ns |  3.37 |    0.01 |    6 |         - |          NA |
| Send_8Behaviors | 9.056 ns | 0.0155 ns | 0.0137 ns |  4.76 |    0.02 |    7 |         - |          NA |
