```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method              | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|-------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DispatchR_ColdStart | 1.857 μs | 0.0081 μs | 0.0075 μs |    1 | 0.6866 | 0.0191 |   8.77 KB |
