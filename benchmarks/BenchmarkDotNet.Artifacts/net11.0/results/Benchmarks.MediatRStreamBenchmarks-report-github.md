```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.4 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method         | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  30.43 ns | 0.178 ns | 0.166 ns |  1.00 |    0.00 |    1 | 0.0067 |      88 B |        1.00 |
| MediatR_Stream | 106.23 ns | 0.232 ns | 0.217 ns |  3.49 |    0.02 |    2 | 0.0354 |     464 B |        5.27 |
