```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                  | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   8.217 ns | 0.0418 ns | 0.0612 ns |   8.235 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 101.646 ns | 0.2219 ns | 0.3321 ns | 101.600 ns | 12.37 |    0.10 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 141.581 ns | 0.2893 ns | 0.4330 ns | 141.641 ns | 17.23 |    0.14 |    3 | 0.0832 |    1088 B |        7.56 |
