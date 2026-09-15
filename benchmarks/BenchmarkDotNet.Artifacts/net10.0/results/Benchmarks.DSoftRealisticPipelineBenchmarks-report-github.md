```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline | 664.5 ns | 3.58 ns | 3.34 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| DSoft_RealisticPipeline | 686.8 ns | 4.25 ns | 3.98 ns |  1.03 |    2 | 0.0191 |     254 B |        0.94 |
