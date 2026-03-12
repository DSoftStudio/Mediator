```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 44.92 ns | 0.924 ns | 0.949 ns |  1.00 |    0.03 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 70.79 ns | 0.465 ns | 0.435 ns |  1.58 |    0.03 |    2 | 0.0176 |     232 B |        1.00 |
