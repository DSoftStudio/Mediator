```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3


```
| Method               | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 9.911 μs | 0.0582 μs | 0.0544 μs |    1 | 2.5330 | 0.1984 |   32.4 KB |
