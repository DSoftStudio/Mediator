```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.23 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                  | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   1.898 ns | 0.0056 ns | 0.0084 ns |   1.899 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Send_3Behaviors | 107.288 ns | 0.6783 ns | 1.0152 ns | 107.221 ns | 56.54 |    0.58 |    2 | 0.0575 |     752 B |          NA |
| MediatR_Send_5Behaviors | 143.534 ns | 1.1071 ns | 1.6228 ns | 143.570 ns | 75.64 |    0.90 |    3 | 0.0782 |    1024 B |          NA |
