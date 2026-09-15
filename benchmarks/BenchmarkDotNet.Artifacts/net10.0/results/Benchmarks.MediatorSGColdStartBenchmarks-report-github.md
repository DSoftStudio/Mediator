```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method               | Mean     | Error    | StdDev   | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|---------:|---------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 40.00 μs | 0.230 μs | 0.215 μs |    1 | 8.9722 | 2.2583 | 114.72 KB |
