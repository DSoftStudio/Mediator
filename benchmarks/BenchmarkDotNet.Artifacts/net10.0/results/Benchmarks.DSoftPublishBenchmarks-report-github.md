```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method         | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish | 3.065 ns | 0.0458 ns | 0.0428 ns |  1.00 |    0.02 |    1 |         - |          NA |
| DSoft_Publish  | 3.276 ns | 0.0437 ns | 0.0409 ns |  1.07 |    0.02 |    2 |         - |          NA |
