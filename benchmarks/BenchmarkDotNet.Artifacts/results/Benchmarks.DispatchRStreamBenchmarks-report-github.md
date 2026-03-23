```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method           | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 46.84 ns | 0.204 ns | 0.170 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 68.08 ns | 0.120 ns | 0.100 ns |  1.45 |    2 | 0.0176 |     232 B |        1.00 |
