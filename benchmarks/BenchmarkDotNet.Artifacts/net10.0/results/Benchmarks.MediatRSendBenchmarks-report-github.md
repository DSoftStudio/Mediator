```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                  | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   8.847 ns | 0.0478 ns | 0.0424 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 108.240 ns | 0.2639 ns | 0.2469 ns | 12.23 |    0.06 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 152.104 ns | 0.5006 ns | 0.4683 ns | 17.19 |    0.09 |    3 | 0.0832 |    1088 B |        7.56 |
