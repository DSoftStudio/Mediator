```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.29 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                |  1.883 ns | 0.0027 ns | 0.0024 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Send_5Behaviors | 49.538 ns | 0.1137 ns | 0.1008 ns | 26.31 |    0.06 |    2 |         - |          NA |
| DispatchR_Send_3Behaviors | 50.272 ns | 0.0909 ns | 0.0759 ns | 26.69 |    0.05 |    2 |         - |          NA |
