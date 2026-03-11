# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
```

## DSoft - Send (No Behaviors)

| Method     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall |  6.668 ns | 0.1491 ns | 0.1245 ns |  1.00 |    0.03 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 17.974 ns | 0.0568 ns | 0.0531 ns |  2.70 |    0.05 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Behaviors)

| Method                | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall            |  5.746 ns | 0.0370 ns | 0.0328 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send            | 12.091 ns | 0.0355 ns | 0.0315 ns |  2.10 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 34.633 ns | 0.0897 ns | 0.0839 ns |  6.03 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 39.381 ns | 0.0665 ns | 0.0622 ns |  6.85 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |

## MediatR - Send (No Behaviors)

| Method       | Mean     | Error    | StdDev   | Rank | Gen0   | Allocated |
|------------- |---------:|---------:|---------:|-----:|-------:|----------:|
| MediatR_Send | 46.81 ns | 0.239 ns | 0.212 ns |    1 | 0.0208 |     272 B |

## MediatR - Send (Behaviors)

| Method                  | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| MediatR_Send            |  44.52 ns | 0.901 ns | 0.964 ns |  1.00 |    0.03 |    1 | 0.0208 |     272 B |        1.00 |
| MediatR_Send_3Behaviors | 107.88 ns | 0.571 ns | 0.534 ns |  2.42 |    0.05 |    2 | 0.0612 |     800 B |        2.94 |
| MediatR_Send_5Behaviors | 153.65 ns | 1.315 ns | 1.230 ns |  3.45 |    0.08 |    3 | 0.0832 |    1088 B |        4.00 |

## DispatchR - Send (No Behaviors)

| Method         | Mean     | Error    | StdDev   | Rank | Gen0   | Allocated |
|--------------- |---------:|---------:|---------:|-----:|-------:|----------:|
| DispatchR_Send | 34.46 ns | 0.092 ns | 0.076 ns |    1 | 0.0055 |      72 B |

## DispatchR - Send (Behaviors)

| Method                    | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DispatchR_Send            | 35.21 ns | 0.140 ns | 0.124 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 52.92 ns | 0.077 ns | 0.072 ns |  1.50 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 53.50 ns | 0.165 ns | 0.155 ns |  1.52 |    2 | 0.0055 |      72 B |        1.00 |

## Publish

| Method            | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish    |   3.486 ns | 0.0199 ns | 0.0177 ns |  1.00 |    0.01 |    1 |      - |         - |          NA |
| DSoft_Publish     |  18.443 ns | 0.0374 ns | 0.0350 ns |  5.29 |    0.03 |    2 |      - |         - |          NA |
| DispatchR_Publish |  35.225 ns | 0.0563 ns | 0.0500 ns | 10.10 |    0.05 |    3 |      - |         - |          NA |
| MediatR_Publish   | 124.263 ns | 0.4466 ns | 0.4177 ns | 35.64 |    0.21 |    4 | 0.0587 |     768 B |          NA |

## Stream

| Method           | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    |  45.44 ns | 0.226 ns | 0.201 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DSoft_Stream     |  56.36 ns | 0.179 ns | 0.168 ns |  1.24 |    2 | 0.0220 |     288 B |        1.24 |
| DispatchR_Stream |  68.31 ns | 0.161 ns | 0.151 ns |  1.50 |    3 | 0.0176 |     232 B |        1.00 |
| MediatR_Stream   | 123.11 ns | 0.411 ns | 0.385 ns |  2.71 |    4 | 0.0477 |     624 B |        2.69 |

## Concurrency

| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,308.91 ns |  5.645 ns |  5.004 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut         | FanOut     | 2,004.99 ns |  6.939 ns |  6.151 ns |  1.53 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,705.80 ns |  5.276 ns |  4.936 ns |  2.83 |    0.01 |    3 | 0.6523 | 0.0153 |    8536 B |        1.00 |
| MediatR_FanOut       | FanOut     | 4,566.08 ns | 13.949 ns | 13.048 ns |  3.49 |    0.02 |    4 | 1.6251 | 0.0381 |   21336 B |        2.50 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    89.86 ns |  0.404 ns |  0.358 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput     | Throughput |   718.02 ns |  0.910 ns |  0.851 ns |  7.99 |    0.03 |    2 | 0.0048 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,030.77 ns |  3.078 ns |  2.879 ns | 33.73 |    0.13 |    3 | 0.0038 |      - |      72 B |        1.00 |
| MediatR_Throughput   | Throughput | 3,603.61 ns | 11.144 ns | 10.424 ns | 40.10 |    0.19 |    4 | 1.5335 |      - |   20072 B |      278.78 |

## Cold Start

| Method              | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_ColdStart     | 1.621 μs | 0.0168 μs | 0.0140 μs |  1.00 |    0.01 |    1 | 0.7172 | 0.0153 |   9.23 KB |        1.00 |
| DispatchR_ColdStart | 1.799 μs | 0.0139 μs | 0.0130 μs |  1.11 |    0.01 |    2 | 0.6714 | 0.0153 |   8.76 KB |        0.95 |
| MediatR_ColdStart   | 3.114 μs | 0.0128 μs | 0.0120 μs |  1.92 |    0.02 |    3 | 0.9766 | 0.0343 |   12.5 KB |        1.35 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/results/`.
