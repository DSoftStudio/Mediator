```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.37 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                  | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   1.812 ns | 0.0144 ns | 0.0135 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Send_3Behaviors | 109.754 ns | 0.3491 ns | 0.2915 ns | 60.57 |    0.46 |    2 | 0.0575 |     752 B |          NA |
| MediatR_Send_5Behaviors | 140.198 ns | 0.6573 ns | 0.5489 ns | 77.37 |    0.63 |    3 | 0.0782 |    1024 B |          NA |
