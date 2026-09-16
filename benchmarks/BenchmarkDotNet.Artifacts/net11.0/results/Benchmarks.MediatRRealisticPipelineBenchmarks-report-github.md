```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.3 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   |  98.57 ns | 0.345 ns | 0.495 ns |  1.00 |    0.00 |    1 | 0.0129 |     168 B |        1.00 |
| MediatR_RealisticPipeline | 350.62 ns | 0.585 ns | 0.781 ns |  3.56 |    0.02 |    2 | 0.0777 |    1016 B |        6.05 |
