```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send | 5.668 ns | 0.0126 ns | 0.0181 ns |  0.96 |    1 | 0.0055 |      72 B |        1.00 |
| DirectCall | 5.882 ns | 0.0251 ns | 0.0375 ns |  1.00 |    2 | 0.0055 |      72 B |        1.00 |
