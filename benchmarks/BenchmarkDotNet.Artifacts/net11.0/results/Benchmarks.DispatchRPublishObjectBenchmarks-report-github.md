```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.31 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  31.54 ns | 0.131 ns | 0.109 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 198.99 ns | 0.477 ns | 0.423 ns |  6.31 |    0.02 |    2 | 0.0196 |     256 B |          NA |
