```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method            | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     | 45.01 ns | 0.152 ns | 0.222 ns | 44.96 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatorSG_Stream | 45.25 ns | 0.268 ns | 0.401 ns | 45.15 ns |  1.01 |    1 | 0.0177 |     232 B |        1.00 |
