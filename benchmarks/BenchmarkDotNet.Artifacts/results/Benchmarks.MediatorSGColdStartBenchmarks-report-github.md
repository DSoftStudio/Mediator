```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3


```
| Method               | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 6.447 μs | 0.0375 μs | 0.0351 μs |    1 | 1.7776 | 0.0992 |  22.76 KB |
