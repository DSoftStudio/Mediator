```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall | 6.825 ns | 0.0331 ns | 0.0310 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 6.844 ns | 0.0386 ns | 0.0342 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
