```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method                  | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |  10.17 ns | 0.080 ns | 0.075 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 108.13 ns | 0.301 ns | 0.267 ns | 10.63 |    0.08 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 153.13 ns | 0.662 ns | 0.619 ns | 15.05 |    0.12 |    3 | 0.0832 |    1088 B |        7.56 |
