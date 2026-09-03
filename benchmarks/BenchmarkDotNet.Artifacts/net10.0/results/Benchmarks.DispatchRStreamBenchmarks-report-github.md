```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method           | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 45.15 ns | 0.402 ns | 0.376 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 68.26 ns | 0.316 ns | 0.280 ns |  1.51 |    2 | 0.0176 |     232 B |        1.00 |
