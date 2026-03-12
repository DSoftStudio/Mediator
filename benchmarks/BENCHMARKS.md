# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8037/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.200
  [Host]     : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v3
```

> **Note:** Per-library sections run in **isolated processes** (only that library active),
> while `All Libraries` sections run with **all mediators initialized** in the same process.
> Absolute numbers may differ between isolated and combined runs due to GC pressure, cache
> contention, and TLS overhead --- compare **Ratio** columns within each table, not across tables.

## DSoft - Send (No Behaviors)

| Method     | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall | 7.511 ns | 0.1202 ns | 0.1125 ns |  1.00 |    0.02 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 8.039 ns | 0.1880 ns | 0.2165 ns |  1.07 |    0.03 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Behaviors)

| Method                | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.646 ns | 0.0289 ns | 0.0270 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send            |  7.886 ns | 0.1765 ns | 0.1651 ns |  1.19 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 15.306 ns | 0.2072 ns | 0.1938 ns |  2.30 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 16.544 ns | 0.2509 ns | 0.2347 ns |  2.49 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |

## MediatR - Send (No Behaviors)

| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   | 10.08 ns | 0.061 ns | 0.057 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send | 40.88 ns | 0.100 ns | 0.088 ns |  4.06 |    0.02 |    2 | 0.0208 |     272 B |        1.89 |

## MediatR - Send (Behaviors)

| Method                  | Mean      | Error    | StdDev   | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |  10.21 ns | 0.234 ns | 0.468 ns |   9.997 ns |  1.00 |    0.06 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send            |  41.89 ns | 0.628 ns | 0.556 ns |  41.647 ns |  4.11 |    0.19 |    2 | 0.0208 |     272 B |        1.89 |
| MediatR_Send_3Behaviors | 111.24 ns | 0.318 ns | 0.265 ns | 111.301 ns | 10.91 |    0.47 |    3 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 161.50 ns | 3.163 ns | 2.959 ns | 161.061 ns | 15.84 |    0.74 |    4 | 0.0832 |    1088 B |        7.56 |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall     |  7.043 ns | 0.0756 ns | 0.0631 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send | 33.349 ns | 0.0738 ns | 0.0616 ns |  4.74 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  7.124 ns | 0.0539 ns | 0.0504 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send            | 34.653 ns | 0.1325 ns | 0.1175 ns |  4.86 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 52.891 ns | 0.0945 ns | 0.0884 ns |  7.42 |    0.05 |    3 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 55.455 ns | 0.0528 ns | 0.0412 ns |  7.78 |    0.05 |    4 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  6.883 ns | 0.0266 ns | 0.0249 ns |  1.00 |    0.00 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 12.530 ns | 0.1404 ns | 0.1313 ns |  1.82 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  7.708 ns | 0.0682 ns | 0.0638 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send            | 13.263 ns | 0.0464 ns | 0.0434 ns |  1.72 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 28.645 ns | 0.0607 ns | 0.0568 ns |  3.72 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 38.099 ns | 0.0424 ns | 0.0397 ns |  4.94 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |

## Send - All Libraries (No Behaviors)

| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Send     |  6.493 ns | 0.0234 ns | 0.0208 ns |  1.00 |    0.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send      |  7.077 ns | 0.0518 ns | 0.0484 ns |  1.09 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 12.538 ns | 0.0350 ns | 0.0328 ns |  1.93 |    0.01 |    3 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send  | 33.374 ns | 0.1147 ns | 0.1017 ns |  5.14 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
| MediatR_Send    | 42.060 ns | 0.1033 ns | 0.0916 ns |  6.48 |    0.02 |    5 | 0.0208 |     272 B |        3.78 |

## Send - All Libraries (Behaviors)

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Send     |   6.792 ns | 0.0391 ns | 0.0366 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send      |  15.536 ns | 0.0328 ns | 0.0307 ns |  2.29 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send |  21.227 ns | 0.0613 ns | 0.0574 ns |  3.13 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send  |  53.481 ns | 0.0818 ns | 0.0725 ns |  7.87 |    0.04 |    4 | 0.0055 |      72 B |        1.00 |
| MediatR_Send    | 150.202 ns | 0.2011 ns | 0.1783 ns | 22.12 |    0.12 |    5 | 0.0832 |    1088 B |       15.11 |

## Publish

| Method             | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish     |   3.712 ns | 0.0467 ns | 0.0436 ns |  1.00 |    0.02 |    1 |      - |         - |          NA |
| DSoft_Publish      |   8.526 ns | 0.0222 ns | 0.0208 ns |  2.30 |    0.03 |    2 |      - |         - |          NA |
| MediatorSG_Publish |  10.170 ns | 0.0167 ns | 0.0148 ns |  2.74 |    0.03 |    3 |      - |         - |          NA |
| DispatchR_Publish  |  34.967 ns | 0.1144 ns | 0.1070 ns |  9.42 |    0.11 |    4 |      - |         - |          NA |
| MediatR_Publish    | 136.145 ns | 0.4880 ns | 0.4326 ns | 36.68 |    0.44 |    5 | 0.0587 |     768 B |          NA |

## Stream

| Method            | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     |  43.74 ns | 0.067 ns | 0.063 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatorSG_Stream |  45.30 ns | 0.148 ns | 0.138 ns |  1.04 |    2 | 0.0177 |     232 B |        1.00 |
| DSoft_Stream      |  45.75 ns | 0.107 ns | 0.100 ns |  1.05 |    2 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream  |  67.13 ns | 0.167 ns | 0.156 ns |  1.53 |    3 | 0.0176 |     232 B |        1.00 |
| MediatR_Stream    | 124.21 ns | 0.316 ns | 0.280 ns |  2.84 |    4 | 0.0477 |     624 B |        2.69 |

## Concurrency

| Method                | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,441.58 ns | 18.340 ns | 17.155 ns |  1.00 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut          | FanOut     | 1,476.96 ns | 27.747 ns | 25.954 ns |  1.02 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,697.04 ns | 24.826 ns | 23.222 ns |  1.18 |    0.02 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut      | FanOut     | 3,805.10 ns | 32.608 ns | 30.501 ns |  2.64 |    0.04 |    3 | 0.6523 | 0.0153 |    8536 B |        1.00 |
| MediatR_FanOut        | FanOut     | 4,812.04 ns | 38.008 ns | 35.553 ns |  3.34 |    0.05 |    4 | 1.6251 | 0.0381 |   21336 B |        2.50 |
|                       |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    85.73 ns |  0.460 ns |  0.408 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput      | Throughput |   193.64 ns |  0.694 ns |  0.649 ns |  2.26 |    0.01 |    2 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput |   868.61 ns |  0.988 ns |  0.924 ns | 10.13 |    0.05 |    3 | 0.0048 |      - |      72 B |        1.00 |
| DispatchR_Throughput  | Throughput | 3,010.34 ns |  3.954 ns |  3.698 ns | 35.12 |    0.17 |    4 | 0.0038 |      - |      72 B |        1.00 |
| MediatR_Throughput    | Throughput | 3,644.80 ns | 11.132 ns | 10.413 ns | 42.52 |    0.23 |    5 | 1.5335 |      - |   20072 B |      278.78 |

## Cold Start

| Method               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_ColdStart      | 1.630 μs | 0.0105 μs | 0.0099 μs |  1.00 |    0.01 |    1 | 0.7229 | 0.0401 |   9.23 KB |        1.00 |
| DispatchR_ColdStart  | 1.905 μs | 0.0318 μs | 0.0282 μs |  1.17 |    0.02 |    2 | 0.6847 | 0.0191 |   8.76 KB |        0.95 |
| MediatR_ColdStart    | 3.097 μs | 0.0469 μs | 0.0416 μs |  1.90 |    0.03 |    3 | 0.9766 | 0.0343 |   12.5 KB |        1.35 |
| MediatorSG_ColdStart | 7.414 μs | 0.0327 μs | 0.0306 μs |  4.55 |    0.03 |    4 | 2.1667 | 0.1526 |   27.8 KB |        3.01 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/results/`.
