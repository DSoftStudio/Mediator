```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.24 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 1.346 ns | 0.0032 ns | 0.0029 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 2.349 ns | 0.0042 ns | 0.0040 ns |  1.75 |    2 |         - |          NA |
