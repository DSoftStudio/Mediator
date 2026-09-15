```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method          | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|---------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DSoft_ColdStart | 2.602 μs | 0.0224 μs | 0.0210 μs |    1 | 1.2283 | 0.0496 |  15.68 KB |
