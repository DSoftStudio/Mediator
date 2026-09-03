```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.888 ns | 0.0256 ns | 0.0239 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 11.410 ns | 0.0271 ns | 0.0227 ns |  1.66 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 11.415 ns | 0.0411 ns | 0.0384 ns |  1.66 |    2 | 0.0055 |      72 B |        1.00 |
