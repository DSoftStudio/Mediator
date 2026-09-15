# Benchmarks

Target framework: `net10.0`

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12
```

> **Note:** Each library's benchmarks run in **isolated processes** (only that library active).
> The `All Libraries` sections below concatenate those isolated results for easy comparison.

## DSoft - Send (No Behaviors)

| Method     | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall | 5.646 ns | 0.0329 ns | 0.0292 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 6.276 ns | 0.1451 ns | 0.2035 ns |  1.11 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Behaviors)

| Method                | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.241 ns | 0.0548 ns | 0.0820 ns |  6.218 ns |  1.00 |    0.02 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 11.966 ns | 0.2559 ns | 0.3503 ns | 11.810 ns |  1.92 |    0.06 |    3 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 11.490 ns | 0.0247 ns | 0.0362 ns | 11.484 ns |  1.84 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Object)

| Method             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  6.127 ns | 0.0326 ns | 0.0305 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 19.632 ns | 0.0743 ns | 0.0695 ns |  3.20 |    0.02 |    2 | 0.0073 |      96 B |        1.33 |

## DSoft - Publish

| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 3.341 ns | 0.0218 ns | 0.0204 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 3.385 ns | 0.0234 ns | 0.0219 ns |  1.01 |    1 |         - |          NA |

## DSoft - Publish (Object)

| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 3.392 ns | 0.0118 ns | 0.0110 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish_Object  | 4.886 ns | 0.0197 ns | 0.0184 ns |  1.44 |    2 |         - |          NA |

## DSoft - Stream

| Method        | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Stream  | 45.92 ns | 0.347 ns | 0.486 ns | 45.82 ns |  0.96 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream | 47.90 ns | 0.340 ns | 0.509 ns | 48.14 ns |  1.00 |    2 | 0.0177 |     232 B |        1.00 |

## DSoft - Concurrency

| Method            | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,377.70 ns | 18.171 ns | 16.997 ns |  0.99 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,397.93 ns | 22.980 ns | 21.495 ns |  1.00 |    0.02 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    85.81 ns |  0.474 ns |  0.420 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   116.88 ns |  0.437 ns |  0.388 ns |  1.36 |    0.01 |    2 | 0.0055 |      - |      72 B |        1.00 |

## DSoft - Cold Start

| Method          | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|---------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DSoft_ColdStart | 2.602 μs | 0.0224 μs | 0.0210 μs |    1 | 1.2283 | 0.0496 |  15.68 KB |

## DSoft - Realistic Pipeline

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline | 664.5 ns | 3.58 ns | 3.34 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| DSoft_RealisticPipeline | 686.8 ns | 4.25 ns | 3.98 ns |  1.03 |    2 | 0.0191 |     254 B |        0.94 |

## DSoft - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.813 ns | 0.0209 ns | 0.0312 ns |  5.816 ns |  1.00 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors |  5.606 ns | 0.0223 ns | 0.0312 ns |  5.603 ns |  0.96 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 12.055 ns | 0.0220 ns | 0.0330 ns | 12.057 ns |  2.07 |    0.01 |    4 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 17.717 ns | 0.0209 ns | 0.0293 ns | 17.712 ns |  3.05 |    0.02 |    5 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 11.367 ns | 0.0254 ns | 0.0365 ns | 11.355 ns |  1.96 |    0.01 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 11.334 ns | 0.0191 ns | 0.0248 ns | 11.342 ns |  1.95 |    0.01 |    3 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 12.027 ns | 0.0239 ns | 0.0335 ns | 12.019 ns |  2.07 |    0.01 |    4 | 0.0055 |      72 B |        1.00 |

## MediatR - Send (No Behaviors)

| Method       | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   |  8.201 ns | 0.0542 ns | 0.0480 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send | 42.346 ns | 0.1185 ns | 0.1109 ns |  5.16 |    0.03 |    2 | 0.0208 |     272 B |        1.89 |

## MediatR - Send (Behaviors)

| Method                  | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   8.393 ns | 0.0359 ns | 0.0537 ns |   8.398 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 100.085 ns | 0.2366 ns | 0.3541 ns | 100.101 ns | 11.93 |    0.09 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 140.428 ns | 0.2321 ns | 0.3473 ns | 140.382 ns | 16.73 |    0.11 |    3 | 0.0832 |    1088 B |        7.56 |

## MediatR - Send (Object)

| Method               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Send_Generic | 43.42 ns | 0.108 ns | 0.090 ns |  1.00 |    1 | 0.0208 |     272 B |        1.00 |
| MediatR_Send_Object  | 46.39 ns | 0.405 ns | 0.379 ns |  1.07 |    2 | 0.0281 |     368 B |        1.35 |

## MediatR - Publish

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish  |   3.306 ns | 0.0179 ns | 0.0167 ns |  1.00 |    0.01 |    1 |      - |         - |          NA |
| MediatR_Publish | 119.325 ns | 2.1274 ns | 1.9899 ns | 36.10 |    0.61 |    2 | 0.0587 |     768 B |          NA |

## MediatR - Publish (Object)

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 112.9 ns | 1.21 ns | 1.13 ns |  0.94 |    1 | 0.0587 |     768 B |        1.00 |
| MediatR_Publish_Generic | 120.2 ns | 1.00 ns | 0.93 ns |  1.00 |    2 | 0.0587 |     768 B |        1.00 |

## MediatR - Stream

| Method         | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  44.95 ns | 0.169 ns | 0.253 ns |  45.00 ns |  1.00 |    0.01 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 123.03 ns | 0.209 ns | 0.294 ns | 122.99 ns |  2.74 |    0.02 |    2 | 0.0477 |     624 B |        2.69 |

## MediatR - Concurrency

| Method             | Categories | Mean       | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |-----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,254.9 ns | 11.55 ns | 10.81 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |   8.34 KB |        1.00 |
| MediatR_FanOut     | FanOut     | 4,638.8 ns | 38.90 ns | 36.38 ns |  3.70 |    0.04 |    2 | 1.6251 | 0.0381 |  20.84 KB |        2.50 |
|                    |            |            |          |          |       |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |   392.4 ns |  1.65 ns |  1.55 ns |  1.00 |    0.01 |    1 | 0.5560 |      - |    7.1 KB |        1.00 |
| MediatR_Throughput | Throughput | 3,944.0 ns | 23.38 ns | 21.87 ns | 10.05 |    0.07 |    2 | 1.5335 |      - |   19.6 KB |        2.76 |

## MediatR - Cold Start

| Method            | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|------------------ |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatR_ColdStart | 3.536 μs | 0.0313 μs | 0.0293 μs |    1 | 0.9918 | 0.0343 |  12.67 KB |

## MediatR - Realistic Pipeline

| Method                    | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   | 683.0 ns | 2.89 ns | 2.71 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatR_RealisticPipeline | 872.0 ns | 5.63 ns | 4.99 ns |  1.28 |    2 | 0.0782 |    1032 B |        3.82 |

## MediatR - Behavior Scaling

| Method          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DirectCall      |   8.311 ns | 0.0466 ns | 0.0698 ns |   8.298 ns |  1.00 |    0.01 |    1 | 0.0110 |      - |     144 B |        1.00 |
| Send_0Behaviors |  42.073 ns | 0.1262 ns | 0.1889 ns |  42.092 ns |  5.06 |    0.05 |    2 | 0.0208 |      - |     272 B |        1.89 |
| Send_1Behaviors |  70.220 ns | 0.7419 ns | 1.1104 ns |  69.806 ns |  8.45 |    0.15 |    3 | 0.0391 |      - |     512 B |        3.56 |
| Send_2Behaviors |  87.607 ns | 0.5157 ns | 0.7719 ns |  87.736 ns | 10.54 |    0.13 |    4 | 0.0502 |      - |     656 B |        4.56 |
| Send_3Behaviors | 101.197 ns | 0.4800 ns | 0.7184 ns | 101.086 ns | 12.18 |    0.13 |    5 | 0.0612 |      - |     800 B |        5.56 |
| Send_5Behaviors | 141.551 ns | 1.2628 ns | 1.8901 ns | 141.338 ns | 17.03 |    0.26 |    6 | 0.0832 |      - |    1088 B |        7.56 |
| Send_8Behaviors | 187.074 ns | 0.6931 ns | 0.9717 ns | 187.026 ns | 22.51 |    0.22 |    7 | 0.1161 | 0.0002 |    1520 B |       10.56 |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall     |  5.500 ns | 0.0690 ns | 0.0645 ns |  1.00 |    0.02 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send | 34.219 ns | 0.2120 ns | 0.1879 ns |  6.22 |    0.08 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  5.824 ns | 0.0296 ns | 0.0434 ns |  5.815 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 34.972 ns | 0.1617 ns | 0.2319 ns | 34.911 ns |  6.01 |    0.06 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 42.786 ns | 2.2194 ns | 3.3219 ns | 43.975 ns |  7.35 |    0.56 |    3 | 0.0055 |      72 B |        1.00 |

## DispatchR - Publish

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  2.928 ns | 0.0096 ns | 0.0090 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Publish | 34.906 ns | 0.1459 ns | 0.1218 ns | 11.92 |    0.05 |    2 |         - |          NA |

## DispatchR - Publish (Object)

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  35.85 ns | 0.112 ns | 0.099 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 231.06 ns | 2.416 ns | 2.260 ns |  6.45 |    0.06 |    2 | 0.0196 |     256 B |          NA |

## DispatchR - Stream

| Method           | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 45.86 ns | 0.359 ns | 0.538 ns | 45.85 ns |  1.00 |    0.02 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 68.07 ns | 0.488 ns | 0.731 ns | 68.01 ns |  1.48 |    0.02 |    2 | 0.0176 |     232 B |        1.00 |

## DispatchR - Concurrency

| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,306.63 ns |  5.004 ns |  4.436 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 4,495.12 ns | 42.499 ns | 39.754 ns |  3.44 |    0.03 |    2 | 0.6485 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    90.17 ns |  0.585 ns |  0.547 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,025.40 ns | 10.126 ns |  9.472 ns | 33.55 |    0.22 |    2 | 0.0038 |      - |      72 B |        1.00 |

## DispatchR - Cold Start

| Method              | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|-------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DispatchR_ColdStart | 2.679 μs | 0.0178 μs | 0.0166 μs |    1 | 1.1978 | 0.0496 |  15.32 KB |

## DispatchR - Realistic Pipeline

| Method                      | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline     | 674.5 ns | 2.39 ns | 2.24 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| DispatchR_RealisticPipeline | 685.9 ns | 4.70 ns | 4.39 ns |  1.02 |    1 | 0.0191 |     255 B |        0.94 |

## DispatchR - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.579 ns | 0.0277 ns | 0.0406 ns |  5.586 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 33.532 ns | 0.2286 ns | 0.3422 ns | 33.437 ns |  6.01 |    0.07 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 34.850 ns | 0.1705 ns | 0.2499 ns | 34.856 ns |  6.25 |    0.06 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 36.921 ns | 0.1641 ns | 0.2405 ns | 36.858 ns |  6.62 |    0.06 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 34.897 ns | 0.2100 ns | 0.3144 ns | 34.760 ns |  6.26 |    0.07 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 41.219 ns | 0.3598 ns | 0.5385 ns | 41.246 ns |  7.39 |    0.11 |    6 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 38.434 ns | 0.2186 ns | 0.3204 ns | 38.325 ns |  6.89 |    0.08 |    5 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.525 ns | 0.0363 ns | 0.0339 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 15.084 ns | 0.0425 ns | 0.0377 ns |  2.73 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  5.539 ns | 0.0246 ns | 0.0369 ns |  5.545 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 22.034 ns | 0.1044 ns | 0.1497 ns | 21.959 ns |  3.98 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 29.607 ns | 0.1024 ns | 0.1469 ns | 29.584 ns |  5.35 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Object)

| Method                  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Send_Generic | 15.63 ns | 0.107 ns | 0.100 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_Object  | 28.24 ns | 0.134 ns | 0.125 ns |  1.81 |    2 | 0.0073 |      96 B |        1.33 |

## Mediator (Source Gen) - Publish

| Method             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish     |  3.067 ns | 0.0311 ns | 0.0291 ns |  1.00 |    0.01 |    1 |         - |          NA |
| MediatorSG_Publish | 18.310 ns | 0.0732 ns | 0.0649 ns |  5.97 |    0.06 |    2 |         - |          NA |

## Mediator (Source Gen) - Publish (Object)

| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  9.009 ns | 0.0219 ns | 0.0183 ns |  0.81 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 11.064 ns | 0.0716 ns | 0.0669 ns |  1.00 |    2 |         - |          NA |

## Mediator (Source Gen) - Stream

| Method            | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     | 45.01 ns | 0.152 ns | 0.222 ns | 44.96 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatorSG_Stream | 45.25 ns | 0.268 ns | 0.401 ns | 45.15 ns |  1.01 |    1 | 0.0177 |     232 B |        1.00 |

## Mediator (Source Gen) - Concurrency

| Method                | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,401.31 ns | 10.284 ns |  9.117 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 2,142.56 ns | 20.806 ns | 18.444 ns |  1.53 |    0.02 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                       |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    92.47 ns |  0.994 ns |  0.929 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput | 1,169.90 ns |  6.476 ns |  6.058 ns | 12.65 |    0.14 |    2 | 0.0038 |      - |      72 B |        1.00 |

## Mediator (Source Gen) - Cold Start

| Method               | Mean     | Error    | StdDev   | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|---------:|---------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 40.00 μs | 0.230 μs | 0.215 μs |    1 | 8.9722 | 2.2583 | 114.72 KB |

## Mediator (Source Gen) - Realistic Pipeline

| Method                       | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      | 676.0 ns | 3.15 ns | 2.80 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatorSG_RealisticPipeline | 737.1 ns | 5.68 ns | 5.31 ns |  1.09 |    2 | 0.0305 |     398 B |        1.47 |

## Mediator (Source Gen) - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.779 ns | 0.2290 ns | 0.3427 ns |  5.704 ns |  1.00 |    0.08 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 15.087 ns | 0.0299 ns | 0.0438 ns | 15.071 ns |  2.62 |    0.15 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 15.829 ns | 0.0284 ns | 0.0416 ns | 15.831 ns |  2.75 |    0.16 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 19.500 ns | 0.0642 ns | 0.0857 ns | 19.481 ns |  3.39 |    0.19 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 22.241 ns | 0.1400 ns | 0.2095 ns | 22.264 ns |  3.86 |    0.22 |    5 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 28.972 ns | 0.2352 ns | 0.3521 ns | 29.011 ns |  5.03 |    0.29 |    6 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 36.088 ns | 0.1914 ns | 0.2806 ns | 36.019 ns |  6.27 |    0.36 |    7 | 0.0055 |      72 B |        1.00 |

## Send - All Libraries (No Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 5.646 ns | 0.0329 ns | 0.0292 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send | 6.276 ns | 0.1451 ns | 0.2035 ns | 1.11 | 0.04 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 8.201 ns | 0.0542 ns | 0.0480 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send | 42.346 ns | 0.1185 ns | 0.1109 ns | 5.16 | 0.03 | 2 | 0.0208 | 272 B | 1.89 |
| | | | | | | | | | |
| DirectCall | 5.500 ns | 0.0690 ns | 0.0645 ns | 1.00 | 0.02 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send | 34.219 ns | 0.2120 ns | 0.1879 ns | 6.22 | 0.08 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 5.525 ns | 0.0363 ns | 0.0339 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send | 15.084 ns | 0.0425 ns | 0.0377 ns | 2.73 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |

## Send - All Libraries (Behaviors)

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 6.241 ns | 0.0548 ns | 0.0820 ns | 6.218 ns | 1.00 | 0.02 | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_3Behaviors | 11.966 ns | 0.2559 ns | 0.3503 ns | 11.810 ns | 1.92 | 0.06 | 3 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_5Behaviors | 11.490 ns | 0.0247 ns | 0.0362 ns | 11.484 ns | 1.84 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | | |
| DirectCall | 8.393 ns | 0.0359 ns | 0.0537 ns | 8.398 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send_3Behaviors | 100.085 ns | 0.2366 ns | 0.3541 ns | 100.101 ns | 11.93 | 0.09 | 2 | 0.0612 | 800 B | 5.56 |
| MediatR_Send_5Behaviors | 140.428 ns | 0.2321 ns | 0.3473 ns | 140.382 ns | 16.73 | 0.11 | 3 | 0.0832 | 1088 B | 7.56 |
| | | | | | | | | | | |
| DirectCall | 5.824 ns | 0.0296 ns | 0.0434 ns | 5.815 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_3Behaviors | 34.972 ns | 0.1617 ns | 0.2319 ns | 34.911 ns | 6.01 | 0.06 | 2 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_5Behaviors | 42.786 ns | 2.2194 ns | 3.3219 ns | 43.975 ns | 7.35 | 0.56 | 3 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | | |
| DirectCall | 5.539 ns | 0.0246 ns | 0.0369 ns | 5.545 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_3Behaviors | 22.034 ns | 0.1044 ns | 0.1497 ns | 21.959 ns | 3.98 | 0.04 | 2 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_5Behaviors | 29.607 ns | 0.1024 ns | 0.1469 ns | 29.584 ns | 5.35 | 0.04 | 3 | 0.0055 | 72 B | 1.00 |

## Send (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send_Generic | 6.127 ns | 0.0326 ns | 0.0305 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_Object | 19.632 ns | 0.0743 ns | 0.0695 ns | 3.20 | 0.02 | 2 | 0.0073 | 96 B | 1.33 |
| | | | | | | | | | |
| MediatR_Send_Generic | 43.42 ns | 0.108 ns | 0.090 ns | 1.00 | - | 1 | 0.0208 | 272 B | 1.00 |
| MediatR_Send_Object | 46.39 ns | 0.405 ns | 0.379 ns | 1.07 | - | 2 | 0.0281 | 368 B | 1.35 |
| | | | | | | | | | |
| MediatorSG_Send_Generic | 15.63 ns | 0.107 ns | 0.100 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_Object | 28.24 ns | 0.134 ns | 0.125 ns | 1.81 | - | 2 | 0.0073 | 96 B | 1.33 |

## Publish - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Publish | 3.341 ns | 0.0218 ns | 0.0204 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish | 3.385 ns | 0.0234 ns | 0.0219 ns | 1.01 | - | 1 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.306 ns | 0.0179 ns | 0.0167 ns | 1.00 | 0.01 | 1 | - | - | NA |
| MediatR_Publish | 119.325 ns | 2.1274 ns | 1.9899 ns | 36.10 | 0.61 | 2 | 0.0587 | 768 B | NA |
| | | | | | | | | | |
| Direct_Publish | 2.928 ns | 0.0096 ns | 0.0090 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish | 34.906 ns | 0.1459 ns | 0.1218 ns | 11.92 | 0.05 | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.067 ns | 0.0311 ns | 0.0291 ns | 1.00 | 0.01 | 1 | - | - | NA |
| MediatorSG_Publish | 18.310 ns | 0.0732 ns | 0.0649 ns | 5.97 | 0.06 | 2 | - | - | NA |

## Publish (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Publish_Generic | 3.392 ns | 0.0118 ns | 0.0110 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish_Object | 4.886 ns | 0.0197 ns | 0.0184 ns | 1.44 | - | 2 | - | - | NA |
| | | | | | | | | | |
| MediatR_Publish_Object | 112.9 ns | 1.21 ns | 1.13 ns | 0.94 | - | 1 | 0.0587 | 768 B | 1.00 |
| MediatR_Publish_Generic | 120.2 ns | 1.00 ns | 0.93 ns | 1.00 | - | 2 | 0.0587 | 768 B | 1.00 |
| | | | | | | | | | |
| DispatchR_Publish_Generic | 35.85 ns | 0.112 ns | 0.099 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish_Object | 231.06 ns | 2.416 ns | 2.260 ns | 6.45 | 0.06 | 2 | 0.0196 | 256 B | NA |
| | | | | | | | | | |
| MediatorSG_Publish_Object | 9.009 ns | 0.0219 ns | 0.0183 ns | 0.81 | - | 1 | - | - | NA |
| MediatorSG_Publish_Generic | 11.064 ns | 0.0716 ns | 0.0669 ns | 1.00 | - | 2 | - | - | NA |

## Stream - All Libraries

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Stream | 45.92 ns | 0.347 ns | 0.486 ns | 45.82 ns | 0.96 | - | 1 | 0.0177 | 232 B | 1.00 |
| Direct_Stream | 47.90 ns | 0.340 ns | 0.509 ns | 48.14 ns | 1.00 | - | 2 | 0.0177 | 232 B | 1.00 |
| | | | | | | | | | | |
| Direct_Stream | 44.95 ns | 0.169 ns | 0.253 ns | 45.00 ns | 1.00 | 0.01 | 1 | 0.0177 | 232 B | 1.00 |
| MediatR_Stream | 123.03 ns | 0.209 ns | 0.294 ns | 122.99 ns | 2.74 | 0.02 | 2 | 0.0477 | 624 B | 2.69 |
| | | | | | | | | | | |
| Direct_Stream | 45.86 ns | 0.359 ns | 0.538 ns | 45.85 ns | 1.00 | 0.02 | 1 | 0.0177 | 232 B | 1.00 |
| DispatchR_Stream | 68.07 ns | 0.488 ns | 0.731 ns | 68.01 ns | 1.48 | 0.02 | 2 | 0.0176 | 232 B | 1.00 |
| | | | | | | | | | | |
| Direct_Stream | 45.01 ns | 0.152 ns | 0.222 ns | 44.96 ns | 1.00 | - | 1 | 0.0177 | 232 B | 1.00 |
| MediatorSG_Stream | 45.25 ns | 0.268 ns | 0.401 ns | 45.15 ns | 1.01 | - | 1 | 0.0177 | 232 B | 1.00 |

## Concurrency - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_FanOut | 1,377.70 ns | 18.171 ns | 16.997 ns | 0.99 | 0.02 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_FanOut | 1,397.93 ns | 22.980 ns | 21.495 ns | 1.00 | 0.02 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 85.81 ns | 0.474 ns | 0.420 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| DSoft_Throughput | 116.88 ns | 0.437 ns | 0.388 ns | 1.36 | 0.01 | 2 | 0.0055 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,254.9 ns | 11.55 ns | 10.81 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8.34 KB | 1.00 |
| MediatR_FanOut | 4,638.8 ns | 38.90 ns | 36.38 ns | 3.70 | 0.04 | 2 | 1.6251 | 0.0381 | 20.84 KB | 2.50 |
| Direct_Throughput | 392.4 ns | 1.65 ns | 1.55 ns | 1.00 | 0.01 | 1 | 0.5560 | - | 7.1 KB | 1.00 |
| MediatR_Throughput | 3,944.0 ns | 23.38 ns | 21.87 ns | 10.05 | 0.07 | 2 | 1.5335 | - | 19.6 KB | 2.76 |
| | | | | | | | | | | |
| Direct_FanOut | 1,306.63 ns | 5.004 ns | 4.436 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DispatchR_FanOut | 4,495.12 ns | 42.499 ns | 39.754 ns | 3.44 | 0.03 | 2 | 0.6485 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 90.17 ns | 0.585 ns | 0.547 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| DispatchR_Throughput | 3,025.40 ns | 10.126 ns | 9.472 ns | 33.55 | 0.22 | 2 | 0.0038 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,401.31 ns | 10.284 ns | 9.117 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatorSG_FanOut | 2,142.56 ns | 20.806 ns | 18.444 ns | 1.53 | 0.02 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 92.47 ns | 0.994 ns | 0.929 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| MediatorSG_Throughput | 1,169.90 ns | 6.476 ns | 6.058 ns | 12.65 | 0.14 | 2 | 0.0038 | - | 72 B | 1.00 |

## Cold Start - All Libraries

| Method | Mean | Error | StdDev | Rank | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_ColdStart | 2.602 μs | 0.0224 μs | 0.0210 μs | 1 | 1.2283 | 0.0496 | 15.68 KB |
| | | | | | | | |
| MediatR_ColdStart | 3.536 μs | 0.0313 μs | 0.0293 μs | 1 | 0.9918 | 0.0343 | 12.67 KB |
| | | | | | | | |
| DispatchR_ColdStart | 2.679 μs | 0.0178 μs | 0.0166 μs | 1 | 1.1978 | 0.0496 | 15.32 KB |
| | | | | | | | |
| MediatorSG_ColdStart | 40.00 μs | 0.230 μs | 0.215 μs | 1 | 8.9722 | 2.2583 | 114.72 KB |

## Realistic Pipeline - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall_WithPipeline | 664.5 ns | 3.58 ns | 3.34 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| DSoft_RealisticPipeline | 686.8 ns | 4.25 ns | 3.98 ns | 1.03 | 2 | 0.0191 | 254 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 683.0 ns | 2.89 ns | 2.71 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| MediatR_RealisticPipeline | 872.0 ns | 5.63 ns | 4.99 ns | 1.28 | 2 | 0.0782 | 1032 B | 3.82 |
| | | | | | | | | |
| DirectCall_WithPipeline | 674.5 ns | 2.39 ns | 2.24 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| DispatchR_RealisticPipeline | 685.9 ns | 4.70 ns | 4.39 ns | 1.02 | 1 | 0.0191 | 255 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 676.0 ns | 3.15 ns | 2.80 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| MediatorSG_RealisticPipeline | 737.1 ns | 5.68 ns | 5.31 ns | 1.09 | 2 | 0.0305 | 398 B | 1.47 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/<tfm>/results/`.
