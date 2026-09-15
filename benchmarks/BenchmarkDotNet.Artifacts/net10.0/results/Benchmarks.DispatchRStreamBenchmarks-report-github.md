```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method           | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 45.86 ns | 0.359 ns | 0.538 ns | 45.85 ns |  1.00 |    0.02 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 68.07 ns | 0.488 ns | 0.731 ns | 68.01 ns |  1.48 |    0.02 |    2 | 0.0176 |     232 B |        1.00 |
