```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  35.85 ns | 0.112 ns | 0.099 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 231.06 ns | 2.416 ns | 2.260 ns |  6.45 |    0.06 |    2 | 0.0196 |     256 B |          NA |
