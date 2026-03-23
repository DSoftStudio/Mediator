```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method             | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  7.290 ns | 0.0226 ns | 0.0200 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 11.346 ns | 0.0219 ns | 0.0194 ns |  1.56 |    2 | 0.0073 |      96 B |        1.33 |
