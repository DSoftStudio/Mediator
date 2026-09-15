```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  6.127 ns | 0.0326 ns | 0.0305 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 19.632 ns | 0.0743 ns | 0.0695 ns |  3.20 |    0.02 |    2 | 0.0073 |      96 B |        1.33 |
