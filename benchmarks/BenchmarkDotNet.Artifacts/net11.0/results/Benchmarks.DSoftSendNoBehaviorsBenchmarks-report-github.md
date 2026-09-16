```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 7.28 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DirectCall | 1.905 ns | 0.0056 ns | 0.0084 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send | 2.678 ns | 0.0027 ns | 0.0039 ns |  1.41 |    2 |         - |          NA |
