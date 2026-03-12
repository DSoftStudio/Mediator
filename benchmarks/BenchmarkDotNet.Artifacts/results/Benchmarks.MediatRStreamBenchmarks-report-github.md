```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method         | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  43.25 ns | 0.125 ns | 0.117 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 121.75 ns | 0.193 ns | 0.151 ns |  2.82 |    2 | 0.0477 |     624 B |        2.69 |
