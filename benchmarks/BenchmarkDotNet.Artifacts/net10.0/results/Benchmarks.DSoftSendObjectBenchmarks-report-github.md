```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method             | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  5.809 ns | 0.0185 ns | 0.0271 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 13.520 ns | 0.0217 ns | 0.0311 ns |  2.33 |    2 | 0.0073 |      96 B |        1.33 |
