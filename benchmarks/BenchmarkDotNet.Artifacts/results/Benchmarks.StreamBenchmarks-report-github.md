```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method            | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     |  43.74 ns | 0.067 ns | 0.063 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatorSG_Stream |  45.30 ns | 0.148 ns | 0.138 ns |  1.04 |    2 | 0.0177 |     232 B |        1.00 |
| DSoft_Stream      |  45.75 ns | 0.107 ns | 0.100 ns |  1.05 |    2 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream  |  67.13 ns | 0.167 ns | 0.156 ns |  1.53 |    3 | 0.0176 |     232 B |        1.00 |
| MediatR_Stream    | 124.21 ns | 0.316 ns | 0.280 ns |  2.84 |    4 | 0.0477 |     624 B |        2.69 |
