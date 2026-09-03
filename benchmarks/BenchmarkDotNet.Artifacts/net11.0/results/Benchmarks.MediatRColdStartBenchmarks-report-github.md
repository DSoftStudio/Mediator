```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.35 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method            | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|------------------ |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatR_ColdStart | 3.212 μs | 0.0203 μs | 0.0190 μs |    1 | 0.9766 | 0.0305 |  12.49 KB |
