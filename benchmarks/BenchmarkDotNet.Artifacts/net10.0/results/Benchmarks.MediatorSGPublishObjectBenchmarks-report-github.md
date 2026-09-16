```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  8.355 ns | 0.0143 ns | 0.0210 ns |  0.75 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 11.084 ns | 0.0227 ns | 0.0339 ns |  1.00 |    2 |         - |          NA |
