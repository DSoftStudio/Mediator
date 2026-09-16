```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DirectCall      |   8.284 ns | 0.0527 ns | 0.0789 ns |   8.273 ns |  1.00 |    0.01 |    1 | 0.0110 |      - |     144 B |        1.00 |
| Send_0Behaviors |  41.029 ns | 0.1238 ns | 0.1775 ns |  41.063 ns |  4.95 |    0.05 |    2 | 0.0208 |      - |     272 B |        1.89 |
| Send_1Behaviors |  69.111 ns | 0.3594 ns | 0.5380 ns |  69.308 ns |  8.34 |    0.10 |    3 | 0.0391 |      - |     512 B |        3.56 |
| Send_2Behaviors |  88.925 ns | 0.2481 ns | 0.3714 ns |  88.877 ns | 10.74 |    0.11 |    4 | 0.0502 |      - |     656 B |        4.56 |
| Send_3Behaviors | 101.778 ns | 1.0137 ns | 1.5173 ns | 101.097 ns | 12.29 |    0.21 |    5 | 0.0612 |      - |     800 B |        5.56 |
| Send_5Behaviors | 138.864 ns | 0.5636 ns | 0.8436 ns | 138.931 ns | 16.76 |    0.19 |    6 | 0.0832 |      - |    1088 B |        7.56 |
| Send_8Behaviors | 182.829 ns | 0.4411 ns | 0.6603 ns | 183.027 ns | 22.07 |    0.22 |    7 | 0.1161 | 0.0002 |    1520 B |       10.56 |
