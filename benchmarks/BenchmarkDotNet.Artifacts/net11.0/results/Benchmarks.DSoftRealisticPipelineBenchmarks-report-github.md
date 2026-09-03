```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.25 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3


```
| Method                  | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline |  92.70 ns | 0.507 ns | 0.474 ns |  1.00 |    1 | 0.0141 |     184 B |        1.00 |
| DSoft_RealisticPipeline | 102.84 ns | 0.404 ns | 0.378 ns |  1.11 |    2 | 0.0122 |     160 B |        0.87 |
