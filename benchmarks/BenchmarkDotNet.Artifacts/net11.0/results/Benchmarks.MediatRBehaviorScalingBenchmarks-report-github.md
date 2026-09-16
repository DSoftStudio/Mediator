```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.24 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12  

```
| Method          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DirectCall      |   1.897 ns | 0.0054 ns | 0.0080 ns |   1.896 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| Send_0Behaviors |  41.210 ns | 0.2373 ns | 0.3551 ns |  41.271 ns | 21.73 |    0.21 |    2 | 0.0190 |      - |     248 B |          NA |
| Send_1Behaviors |  71.802 ns | 0.1944 ns | 0.2910 ns |  71.804 ns | 37.86 |    0.22 |    3 | 0.0367 |      - |     480 B |          NA |
| Send_2Behaviors |  88.604 ns | 0.2083 ns | 0.2780 ns |  88.643 ns | 46.72 |    0.24 |    4 | 0.0471 |      - |     616 B |          NA |
| Send_3Behaviors | 104.571 ns | 0.3013 ns | 0.4416 ns | 104.609 ns | 55.14 |    0.32 |    5 | 0.0575 |      - |     752 B |          NA |
| Send_5Behaviors | 136.119 ns | 0.5388 ns | 0.8064 ns | 136.152 ns | 71.77 |    0.51 |    6 | 0.0782 |      - |    1024 B |          NA |
| Send_8Behaviors | 188.066 ns | 0.5503 ns | 0.8066 ns | 187.977 ns | 99.16 |    0.59 |    7 | 0.1094 | 0.0002 |    1432 B |          NA |
