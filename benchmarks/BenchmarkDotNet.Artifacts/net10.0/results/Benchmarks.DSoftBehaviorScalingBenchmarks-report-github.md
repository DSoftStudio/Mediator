```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method          | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall      |  7.113 ns | 0.0267 ns | 0.0250 ns |  1.00 |    2 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors |  6.830 ns | 0.0490 ns | 0.0458 ns |  0.96 |    1 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 11.464 ns | 0.0246 ns | 0.0218 ns |  1.61 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 11.411 ns | 0.0292 ns | 0.0273 ns |  1.60 |    3 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 11.229 ns | 0.0481 ns | 0.0402 ns |  1.58 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 12.138 ns | 0.0329 ns | 0.0307 ns |  1.71 |    4 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 12.182 ns | 0.0355 ns | 0.0315 ns |  1.71 |    4 | 0.0055 |      72 B |        1.00 |
