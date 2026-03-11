```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method           | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    |  45.44 ns | 0.226 ns | 0.201 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DSoft_Stream     |  56.36 ns | 0.179 ns | 0.168 ns |  1.24 |    2 | 0.0220 |     288 B |        1.24 |
| DispatchR_Stream |  68.31 ns | 0.161 ns | 0.151 ns |  1.50 |    3 | 0.0176 |     232 B |        1.00 |
| MediatR_Stream   | 123.11 ns | 0.411 ns | 0.385 ns |  2.71 |    4 | 0.0477 |     624 B |        2.69 |
