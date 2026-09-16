```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.48 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method           | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 31.15 ns | 0.084 ns | 0.121 ns | 31.16 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| DispatchR_Stream | 54.02 ns | 0.069 ns | 0.103 ns | 54.01 ns |  1.73 |    2 | 0.0067 |      88 B |        1.00 |
