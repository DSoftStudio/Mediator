```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method         | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  44.95 ns | 0.169 ns | 0.253 ns |  45.00 ns |  1.00 |    0.01 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 123.03 ns | 0.209 ns | 0.294 ns | 122.99 ns |  2.74 |    0.02 |    2 | 0.0477 |     624 B |        2.69 |
