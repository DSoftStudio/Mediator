```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method         | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  44.57 ns | 0.210 ns | 0.186 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 122.87 ns | 0.375 ns | 0.351 ns |  2.76 |    2 | 0.0477 |     624 B |        2.69 |
