```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.33 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method            | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     | 30.47 ns | 0.127 ns | 0.119 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| MediatorSG_Stream | 31.71 ns | 0.118 ns | 0.110 ns |  1.04 |    2 | 0.0067 |      88 B |        1.00 |
