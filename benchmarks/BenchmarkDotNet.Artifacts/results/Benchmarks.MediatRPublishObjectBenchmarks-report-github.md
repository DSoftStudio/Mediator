```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                  | Mean     | Error   | StdDev  | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|--------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 126.5 ns | 2.49 ns | 2.33 ns |  1.00 |    0.02 |    1 | 0.0587 |     768 B |        1.00 |
| MediatR_Publish_Generic | 126.9 ns | 0.71 ns | 0.66 ns |  1.00 |    0.01 |    1 | 0.0587 |     768 B |        1.00 |
