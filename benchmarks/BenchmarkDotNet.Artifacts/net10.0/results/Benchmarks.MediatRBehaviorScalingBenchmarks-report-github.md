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
| DirectCall      |   8.311 ns | 0.0466 ns | 0.0698 ns |   8.298 ns |  1.00 |    0.01 |    1 | 0.0110 |      - |     144 B |        1.00 |
| Send_0Behaviors |  42.073 ns | 0.1262 ns | 0.1889 ns |  42.092 ns |  5.06 |    0.05 |    2 | 0.0208 |      - |     272 B |        1.89 |
| Send_1Behaviors |  70.220 ns | 0.7419 ns | 1.1104 ns |  69.806 ns |  8.45 |    0.15 |    3 | 0.0391 |      - |     512 B |        3.56 |
| Send_2Behaviors |  87.607 ns | 0.5157 ns | 0.7719 ns |  87.736 ns | 10.54 |    0.13 |    4 | 0.0502 |      - |     656 B |        4.56 |
| Send_3Behaviors | 101.197 ns | 0.4800 ns | 0.7184 ns | 101.086 ns | 12.18 |    0.13 |    5 | 0.0612 |      - |     800 B |        5.56 |
| Send_5Behaviors | 141.551 ns | 1.2628 ns | 1.8901 ns | 141.338 ns | 17.03 |    0.26 |    6 | 0.0832 |      - |    1088 B |        7.56 |
| Send_8Behaviors | 187.074 ns | 0.6931 ns | 0.9717 ns | 187.026 ns | 22.51 |    0.22 |    7 | 0.1161 | 0.0002 |    1520 B |       10.56 |
