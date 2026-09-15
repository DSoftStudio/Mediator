```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method        | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Stream  | 45.92 ns | 0.347 ns | 0.486 ns | 45.82 ns |  0.96 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream | 47.90 ns | 0.340 ns | 0.509 ns | 48.14 ns |  1.00 |    2 | 0.0177 |     232 B |        1.00 |
