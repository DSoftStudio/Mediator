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

| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send | 5.668 ns | 0.0126 ns | 0.0181 ns |  0.96 |    1 | 0.0055 |      72 B |        1.00 |
| DirectCall | 5.882 ns | 0.0251 ns | 0.0375 ns |  1.00 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Behaviors)

| Method                | Mean      | Error     | StdDev    | Median    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall            |  5.542 ns | 0.0175 ns | 0.0257 ns |  5.540 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 11.172 ns | 0.0172 ns | 0.0257 ns | 11.173 ns |  2.02 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 11.415 ns | 0.0250 ns | 0.0351 ns | 11.411 ns |  2.06 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Object)

| Method             | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  5.809 ns | 0.0185 ns | 0.0271 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 13.520 ns | 0.0217 ns | 0.0311 ns |  2.33 |    2 | 0.0073 |      96 B |        1.33 |

## DSoft - Publish

| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 3.336 ns | 0.0159 ns | 0.0238 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 3.353 ns | 0.0097 ns | 0.0146 ns |  1.01 |    1 |         - |          NA |

## DSoft - Publish (Object)

| Method                | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 3.240 ns | 0.0168 ns | 0.0251 ns |  1.00 |    0.01 |    1 |         - |          NA |
| DSoft_Publish_Object  | 5.303 ns | 0.0661 ns | 0.0989 ns |  1.64 |    0.03 |    2 |         - |          NA |

## DSoft - Stream

| Method        | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Stream  | 44.76 ns | 0.065 ns | 0.089 ns | 44.77 ns |  0.99 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream | 45.33 ns | 0.168 ns | 0.252 ns | 45.30 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |

## DSoft - Concurrency

| Method            | Categories | Mean        | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,274.02 ns | 2.809 ns | 4.205 ns |  0.98 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,296.28 ns | 3.170 ns | 4.745 ns |  1.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |      |        |        |           |             |
| Direct_Throughput | Throughput |    92.99 ns | 0.230 ns | 0.344 ns |  1.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   108.82 ns | 0.301 ns | 0.432 ns |  1.17 |    2 | 0.0055 |      - |      72 B |        1.00 |

## DSoft - Cold Start

> **Read the gap, not the total.** `Startup_ContainerOnly` builds the DI container and resolves the
> mediator. `Startup_WithFirstDispatch` does the same and then dispatches one request. Nearly all of
> either number is .NET runtime startup and DI container construction, which every library on this
> page pays alike. **The difference between the two rows is the part that belongs to the library.**
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean — and
> the first row executed also absorbs the machine's own file-cache warm-up, which inflates it and so
> understates the gap.

| Method                          | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DSoft_Startup_ContainerOnly     | 17.00 ms | 5.541 ms | 9.849 ms | 15.40 ms |  1.08 |    0.64 |    1 |   17.3 KB |        1.00 |
| DSoft_Startup_WithFirstDispatch | 19.92 ms | 0.135 ms | 0.240 ms | 19.92 ms |  1.26 |    0.16 |    2 |  17.37 KB |        1.00 |

## DSoft - Realistic Pipeline

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline | 669.4 ns | 2.47 ns | 3.62 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| DSoft_RealisticPipeline | 682.0 ns | 2.78 ns | 4.15 ns |  1.02 |    1 | 0.0191 |     254 B |        0.94 |

## DSoft - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.953 ns | 0.0335 ns | 0.0502 ns |  5.963 ns |  1.00 |    0.01 |    2 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors |  5.637 ns | 0.0211 ns | 0.0316 ns |  5.639 ns |  0.95 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 11.493 ns | 0.0396 ns | 0.0580 ns | 11.506 ns |  1.93 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 12.124 ns | 0.0215 ns | 0.0315 ns | 12.119 ns |  2.04 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 12.027 ns | 0.0334 ns | 0.0479 ns | 12.031 ns |  2.02 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 11.474 ns | 0.0756 ns | 0.1132 ns | 11.441 ns |  1.93 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 12.048 ns | 0.0202 ns | 0.0295 ns | 12.040 ns |  2.02 |    0.02 |    4 | 0.0055 |      72 B |        1.00 |

## MediatR - Send (No Behaviors)

| Method       | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   |  8.071 ns | 0.0235 ns | 0.0305 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send | 41.656 ns | 0.2899 ns | 0.4338 ns |  5.16 |    0.06 |    2 | 0.0208 |     272 B |        1.89 |

## MediatR - Send (Behaviors)

| Method                  | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   8.217 ns | 0.0418 ns | 0.0612 ns |   8.235 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 101.646 ns | 0.2219 ns | 0.3321 ns | 101.600 ns | 12.37 |    0.10 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 141.581 ns | 0.2893 ns | 0.4330 ns | 141.641 ns | 17.23 |    0.14 |    3 | 0.0832 |    1088 B |        7.56 |

## MediatR - Send (Object)

| Method               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Send_Generic | 43.56 ns | 0.126 ns | 0.189 ns |  1.00 |    1 | 0.0208 |     272 B |        1.00 |
| MediatR_Send_Object  | 46.33 ns | 0.132 ns | 0.198 ns |  1.06 |    2 | 0.0281 |     368 B |        1.35 |

## MediatR - Publish

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish  |   3.057 ns | 0.0240 ns | 0.0359 ns |  1.00 |    0.02 |    1 |      - |         - |          NA |
| MediatR_Publish | 116.464 ns | 0.2825 ns | 0.4228 ns | 38.10 |    0.46 |    2 | 0.0587 |     768 B |          NA |

## MediatR - Publish (Object)

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 113.6 ns | 0.20 ns | 0.29 ns |  0.94 |    1 | 0.0587 |     768 B |        1.00 |
| MediatR_Publish_Generic | 121.2 ns | 0.37 ns | 0.52 ns |  1.00 |    2 | 0.0587 |     768 B |        1.00 |

## MediatR - Stream

| Method         | Mean      | Error    | StdDev   | Median    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|----------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  45.17 ns | 0.134 ns | 0.200 ns |  45.21 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 126.28 ns | 0.202 ns | 0.289 ns | 126.19 ns |  2.80 |    2 | 0.0477 |     624 B |        2.69 |

## MediatR - Concurrency

| Method             | Categories | Mean       | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |-----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,246.1 ns |  3.51 ns |  5.25 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |   8.34 KB |        1.00 |
| MediatR_FanOut     | FanOut     | 4,536.5 ns | 11.57 ns | 17.31 ns |  3.64 |    0.02 |    2 | 1.6251 | 0.0381 |  20.84 KB |        2.50 |
|                    |            |            |          |          |       |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |   384.3 ns |  1.42 ns |  2.12 ns |  1.00 |    0.01 |    1 | 0.5560 |      - |    7.1 KB |        1.00 |
| MediatR_Throughput | Throughput | 3,726.8 ns | 10.18 ns | 15.24 ns |  9.70 |    0.07 |    2 | 1.5335 |      - |   19.6 KB |        2.76 |

## MediatR - Cold Start

> **Read the gap, not the total.** `Startup_ContainerOnly` builds the DI container and resolves the
> mediator. `Startup_WithFirstDispatch` does the same and then dispatches one request. Nearly all of
> either number is .NET runtime startup and DI container construction, which every library on this
> page pays alike. **The difference between the two rows is the part that belongs to the library.**
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean — and
> the first row executed also absorbs the machine's own file-cache warm-up, which inflates it and so
> understates the gap.

| Method                            | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| MediatR_Startup_ContainerOnly     | 34.45 ms | 5.315 ms | 9.447 ms | 32.94 ms |  1.03 |    0.30 |    1 |  12.41 KB |        1.00 |
| MediatR_Startup_WithFirstDispatch | 34.58 ms | 0.204 ms | 0.363 ms | 34.62 ms |  1.03 |    0.11 |    2 |  15.22 KB |        1.23 |

## MediatR - Realistic Pipeline

| Method                    | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   | 668.0 ns | 3.56 ns | 5.22 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| MediatR_RealisticPipeline | 847.7 ns | 3.54 ns | 5.18 ns |  1.27 |    2 | 0.0782 |    1032 B |        3.81 |

## MediatR - Behavior Scaling

| Method          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DirectCall      |   8.284 ns | 0.0527 ns | 0.0789 ns |   8.273 ns |  1.00 |    0.01 |    1 | 0.0110 |      - |     144 B |        1.00 |
| Send_0Behaviors |  41.029 ns | 0.1238 ns | 0.1775 ns |  41.063 ns |  4.95 |    0.05 |    2 | 0.0208 |      - |     272 B |        1.89 |
| Send_1Behaviors |  69.111 ns | 0.3594 ns | 0.5380 ns |  69.308 ns |  8.34 |    0.10 |    3 | 0.0391 |      - |     512 B |        3.56 |
| Send_2Behaviors |  88.925 ns | 0.2481 ns | 0.3714 ns |  88.877 ns | 10.74 |    0.11 |    4 | 0.0502 |      - |     656 B |        4.56 |
| Send_3Behaviors | 101.778 ns | 1.0137 ns | 1.5173 ns | 101.097 ns | 12.29 |    0.21 |    5 | 0.0612 |      - |     800 B |        5.56 |
| Send_5Behaviors | 138.864 ns | 0.5636 ns | 0.8436 ns | 138.931 ns | 16.76 |    0.19 |    6 | 0.0832 |      - |    1088 B |        7.56 |
| Send_8Behaviors | 182.829 ns | 0.4411 ns | 0.6603 ns | 183.027 ns | 22.07 |    0.22 |    7 | 0.1161 | 0.0002 |    1520 B |       10.56 |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall     |  5.359 ns | 0.0314 ns | 0.0470 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send | 33.456 ns | 0.0907 ns | 0.1329 ns |  6.24 |    0.06 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  5.709 ns | 0.0214 ns | 0.0320 ns |  5.710 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 34.498 ns | 0.0642 ns | 0.0941 ns | 34.504 ns |  6.04 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 34.567 ns | 0.0510 ns | 0.0748 ns | 34.560 ns |  6.06 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Publish

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  2.802 ns | 0.0077 ns | 0.0113 ns |  1.00 |    0.01 |    1 |         - |          NA |
| DispatchR_Publish | 35.330 ns | 0.0525 ns | 0.0737 ns | 12.61 |    0.06 |    2 |         - |          NA |

## DispatchR - Publish (Object)

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  35.40 ns | 0.061 ns | 0.091 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 219.09 ns | 0.315 ns | 0.471 ns |  6.19 |    0.02 |    2 | 0.0196 |     256 B |          NA |

## DispatchR - Stream

| Method           | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 43.20 ns | 0.081 ns | 0.118 ns | 43.16 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 67.68 ns | 0.144 ns | 0.215 ns | 67.62 ns |  1.57 |    2 | 0.0176 |     232 B |        1.00 |

## DispatchR - Concurrency

| Method               | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,256.71 ns | 2.806 ns | 4.200 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 4,158.06 ns | 6.061 ns | 8.693 ns |  3.31 |    0.01 |    2 | 0.6485 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    90.42 ns | 0.170 ns | 0.244 ns |  1.00 |    0.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,031.05 ns | 4.604 ns | 6.603 ns | 33.52 |    0.11 |    2 | 0.0038 |      - |      72 B |        1.00 |

## DispatchR - Cold Start

> **Read the gap, not the total.** `Startup_ContainerOnly` builds the DI container and resolves the
> mediator. `Startup_WithFirstDispatch` does the same and then dispatches one request. Nearly all of
> either number is .NET runtime startup and DI container construction, which every library on this
> page pays alike. **The difference between the two rows is the part that belongs to the library.**
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean — and
> the first row executed also absorbs the machine's own file-cache warm-up, which inflates it and so
> understates the gap.

| Method                              | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| DispatchR_Startup_ContainerOnly     | 15.46 ms | 5.314 ms | 9.445 ms | 13.93 ms |  1.08 |    0.67 |    1 |  14.66 KB |        1.00 |
| DispatchR_Startup_WithFirstDispatch | 18.07 ms | 0.098 ms | 0.174 ms | 18.02 ms |  1.27 |    0.16 |    2 |  17.04 KB |        1.16 |

## DispatchR - Realistic Pipeline

| Method                      | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline     | 671.6 ns | 1.54 ns | 2.31 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| DispatchR_RealisticPipeline | 693.8 ns | 2.21 ns | 3.31 ns |  1.03 |    2 | 0.0191 |     255 B |        0.94 |

## DispatchR - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.433 ns | 0.0196 ns | 0.0287 ns |  5.433 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 33.410 ns | 0.0709 ns | 0.1060 ns | 33.408 ns |  6.15 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 35.141 ns | 0.0941 ns | 0.1408 ns | 35.105 ns |  6.47 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 34.384 ns | 0.0607 ns | 0.0908 ns | 34.367 ns |  6.33 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 35.281 ns | 0.0684 ns | 0.1002 ns | 35.245 ns |  6.49 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 34.479 ns | 0.0602 ns | 0.0901 ns | 34.468 ns |  6.35 |    0.04 |    3 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 43.749 ns | 0.0634 ns | 0.0930 ns | 43.736 ns |  8.05 |    0.05 |    4 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.645 ns | 0.0198 ns | 0.0297 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 15.035 ns | 0.0313 ns | 0.0449 ns |  2.66 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  5.708 ns | 0.0171 ns | 0.0246 ns |  5.709 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 22.350 ns | 0.0494 ns | 0.0740 ns | 22.328 ns |  3.92 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 28.931 ns | 0.0408 ns | 0.0545 ns | 28.929 ns |  5.07 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Object)

| Method                  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Send_Generic | 15.46 ns | 0.042 ns | 0.062 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_Object  | 23.41 ns | 0.059 ns | 0.088 ns |  1.51 |    2 | 0.0073 |      96 B |        1.33 |

## Mediator (Source Gen) - Publish

| Method             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish     |  3.077 ns | 0.0118 ns | 0.0173 ns |  1.00 |    0.01 |    1 |         - |          NA |
| MediatorSG_Publish | 10.619 ns | 0.0240 ns | 0.0344 ns |  3.45 |    0.02 |    2 |         - |          NA |

## Mediator (Source Gen) - Publish (Object)

| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  8.355 ns | 0.0143 ns | 0.0210 ns |  0.75 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 11.084 ns | 0.0227 ns | 0.0339 ns |  1.00 |    2 |         - |          NA |

## Mediator (Source Gen) - Stream

| Method            | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Stream | 44.29 ns | 0.075 ns | 0.112 ns | 44.29 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream     | 44.42 ns | 0.126 ns | 0.189 ns | 44.47 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |

## Mediator (Source Gen) - Concurrency

| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,289.45 ns | 6.003 ns | 8.986 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 2,031.01 ns | 3.608 ns | 5.289 ns |  1.58 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    84.97 ns | 0.376 ns | 0.551 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput | 1,209.95 ns | 2.770 ns | 4.060 ns | 14.24 |    0.10 |    2 | 0.0038 |      - |      72 B |        1.00 |

## Mediator (Source Gen) - Cold Start

> **Read the gap, not the total.** `Startup_ContainerOnly` builds the DI container and resolves the
> mediator. `Startup_WithFirstDispatch` does the same and then dispatches one request. Nearly all of
> either number is .NET runtime startup and DI container construction, which every library on this
> page pays alike. **The difference between the two rows is the part that belongs to the library.**
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean — and
> the first row executed also absorbs the machine's own file-cache warm-up, which inflates it and so
> understates the gap.

| Method                               | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| MediatorSG_Startup_ContainerOnly     | 16.02 ms | 5.325 ms | 9.465 ms | 14.52 ms |  1.08 |    0.65 |    1 |  15.51 KB |        1.00 |
| MediatorSG_Startup_WithFirstDispatch | 23.30 ms | 0.196 ms | 0.349 ms | 23.34 ms |  1.57 |    0.20 |    2 | 150.35 KB |        9.70 |

## Mediator (Source Gen) - Realistic Pipeline

| Method                       | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      | 689.4 ns | 4.01 ns | 6.00 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatorSG_RealisticPipeline | 732.4 ns | 3.14 ns | 4.60 ns |  1.06 |    2 | 0.0305 |     398 B |        1.47 |

## Mediator (Source Gen) - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.542 ns | 0.0311 ns | 0.0465 ns |  5.558 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors | 15.027 ns | 0.0273 ns | 0.0373 ns | 15.023 ns |  2.71 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 15.679 ns | 0.0251 ns | 0.0344 ns | 15.683 ns |  2.83 |    0.02 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 18.376 ns | 0.0422 ns | 0.0631 ns | 18.376 ns |  3.32 |    0.03 |    4 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 20.602 ns | 0.0343 ns | 0.0514 ns | 20.591 ns |  3.72 |    0.03 |    5 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 29.472 ns | 0.0586 ns | 0.0876 ns | 29.456 ns |  5.32 |    0.05 |    6 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 36.694 ns | 0.0523 ns | 0.0783 ns | 36.676 ns |  6.62 |    0.06 |    7 | 0.0055 |      72 B |        1.00 |

## Send - All Libraries (No Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send | 5.668 ns | 0.0126 ns | 0.0181 ns | 0.96 | - | 1 | 0.0055 | 72 B | 1.00 |
| DirectCall | 5.882 ns | 0.0251 ns | 0.0375 ns | 1.00 | - | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 8.071 ns | 0.0235 ns | 0.0305 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send | 41.656 ns | 0.2899 ns | 0.4338 ns | 5.16 | 0.06 | 2 | 0.0208 | 272 B | 1.89 |
| | | | | | | | | | |
| DirectCall | 5.359 ns | 0.0314 ns | 0.0470 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send | 33.456 ns | 0.0907 ns | 0.1329 ns | 6.24 | 0.06 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 5.645 ns | 0.0198 ns | 0.0297 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send | 15.035 ns | 0.0313 ns | 0.0449 ns | 2.66 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |

## Send - All Libraries (Behaviors)

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 5.542 ns | 0.0175 ns | 0.0257 ns | 5.540 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_3Behaviors | 11.172 ns | 0.0172 ns | 0.0257 ns | 11.173 ns | 2.02 | - | 2 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_5Behaviors | 11.415 ns | 0.0250 ns | 0.0351 ns | 11.411 ns | 2.06 | - | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | | |
| DirectCall | 8.217 ns | 0.0418 ns | 0.0612 ns | 8.235 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send_3Behaviors | 101.646 ns | 0.2219 ns | 0.3321 ns | 101.600 ns | 12.37 | 0.10 | 2 | 0.0612 | 800 B | 5.56 |
| MediatR_Send_5Behaviors | 141.581 ns | 0.2893 ns | 0.4330 ns | 141.641 ns | 17.23 | 0.14 | 3 | 0.0832 | 1088 B | 7.56 |
| | | | | | | | | | | |
| DirectCall | 5.709 ns | 0.0214 ns | 0.0320 ns | 5.710 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_3Behaviors | 34.498 ns | 0.0642 ns | 0.0941 ns | 34.504 ns | 6.04 | 0.04 | 2 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_5Behaviors | 34.567 ns | 0.0510 ns | 0.0748 ns | 34.560 ns | 6.06 | 0.04 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | | |
| DirectCall | 5.708 ns | 0.0171 ns | 0.0246 ns | 5.709 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_3Behaviors | 22.350 ns | 0.0494 ns | 0.0740 ns | 22.328 ns | 3.92 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_5Behaviors | 28.931 ns | 0.0408 ns | 0.0545 ns | 28.929 ns | 5.07 | 0.02 | 3 | 0.0055 | 72 B | 1.00 |

## Send (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send_Generic | 5.809 ns | 0.0185 ns | 0.0271 ns | 1.00 | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_Object | 13.520 ns | 0.0217 ns | 0.0311 ns | 2.33 | 2 | 0.0073 | 96 B | 1.33 |
| | | | | | | | | |
| MediatR_Send_Generic | 43.56 ns | 0.126 ns | 0.189 ns | 1.00 | 1 | 0.0208 | 272 B | 1.00 |
| MediatR_Send_Object | 46.33 ns | 0.132 ns | 0.198 ns | 1.06 | 2 | 0.0281 | 368 B | 1.35 |
| | | | | | | | | |
| MediatorSG_Send_Generic | 15.46 ns | 0.042 ns | 0.062 ns | 1.00 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_Object | 23.41 ns | 0.059 ns | 0.088 ns | 1.51 | 2 | 0.0073 | 96 B | 1.33 |

## Publish - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Publish | 3.336 ns | 0.0159 ns | 0.0238 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish | 3.353 ns | 0.0097 ns | 0.0146 ns | 1.01 | - | 1 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.057 ns | 0.0240 ns | 0.0359 ns | 1.00 | 0.02 | 1 | - | - | NA |
| MediatR_Publish | 116.464 ns | 0.2825 ns | 0.4228 ns | 38.10 | 0.46 | 2 | 0.0587 | 768 B | NA |
| | | | | | | | | | |
| Direct_Publish | 2.802 ns | 0.0077 ns | 0.0113 ns | 1.00 | 0.01 | 1 | - | - | NA |
| DispatchR_Publish | 35.330 ns | 0.0525 ns | 0.0737 ns | 12.61 | 0.06 | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.077 ns | 0.0118 ns | 0.0173 ns | 1.00 | 0.01 | 1 | - | - | NA |
| MediatorSG_Publish | 10.619 ns | 0.0240 ns | 0.0344 ns | 3.45 | 0.02 | 2 | - | - | NA |

## Publish (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Publish_Generic | 3.240 ns | 0.0168 ns | 0.0251 ns | 1.00 | 0.01 | 1 | - | - | NA |
| DSoft_Publish_Object | 5.303 ns | 0.0661 ns | 0.0989 ns | 1.64 | 0.03 | 2 | - | - | NA |
| | | | | | | | | | |
| MediatR_Publish_Object | 113.6 ns | 0.20 ns | 0.29 ns | 0.94 | - | 1 | 0.0587 | 768 B | 1.00 |
| MediatR_Publish_Generic | 121.2 ns | 0.37 ns | 0.52 ns | 1.00 | - | 2 | 0.0587 | 768 B | 1.00 |
| | | | | | | | | | |
| DispatchR_Publish_Generic | 35.40 ns | 0.061 ns | 0.091 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish_Object | 219.09 ns | 0.315 ns | 0.471 ns | 6.19 | 0.02 | 2 | 0.0196 | 256 B | NA |
| | | | | | | | | | |
| MediatorSG_Publish_Object | 8.355 ns | 0.0143 ns | 0.0210 ns | 0.75 | - | 1 | - | - | NA |
| MediatorSG_Publish_Generic | 11.084 ns | 0.0227 ns | 0.0339 ns | 1.00 | - | 2 | - | - | NA |

## Stream - All Libraries

| Method | Mean | Error | StdDev | Median | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Stream | 44.76 ns | 0.065 ns | 0.089 ns | 44.77 ns | 0.99 | 1 | 0.0177 | 232 B | 1.00 |
| Direct_Stream | 45.33 ns | 0.168 ns | 0.252 ns | 45.30 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| | | | | | | | | | |
| Direct_Stream | 45.17 ns | 0.134 ns | 0.200 ns | 45.21 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| MediatR_Stream | 126.28 ns | 0.202 ns | 0.289 ns | 126.19 ns | 2.80 | 2 | 0.0477 | 624 B | 2.69 |
| | | | | | | | | | |
| Direct_Stream | 43.20 ns | 0.081 ns | 0.118 ns | 43.16 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| DispatchR_Stream | 67.68 ns | 0.144 ns | 0.215 ns | 67.62 ns | 1.57 | 2 | 0.0176 | 232 B | 1.00 |
| | | | | | | | | | |
| MediatorSG_Stream | 44.29 ns | 0.075 ns | 0.112 ns | 44.29 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| Direct_Stream | 44.42 ns | 0.126 ns | 0.189 ns | 44.47 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |

## Concurrency - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_FanOut | 1,274.02 ns | 2.809 ns | 4.205 ns | 0.98 | - | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_FanOut | 1,296.28 ns | 3.170 ns | 4.745 ns | 1.00 | - | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 92.99 ns | 0.230 ns | 0.344 ns | 1.00 | - | 1 | 0.0055 | - | 72 B | 1.00 |
| DSoft_Throughput | 108.82 ns | 0.301 ns | 0.432 ns | 1.17 | - | 2 | 0.0055 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,246.1 ns | 3.51 ns | 5.25 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8.34 KB | 1.00 |
| MediatR_FanOut | 4,536.5 ns | 11.57 ns | 17.31 ns | 3.64 | 0.02 | 2 | 1.6251 | 0.0381 | 20.84 KB | 2.50 |
| Direct_Throughput | 384.3 ns | 1.42 ns | 2.12 ns | 1.00 | 0.01 | 1 | 0.5560 | - | 7.1 KB | 1.00 |
| MediatR_Throughput | 3,726.8 ns | 10.18 ns | 15.24 ns | 9.70 | 0.07 | 2 | 1.5335 | - | 19.6 KB | 2.76 |
| | | | | | | | | | | |
| Direct_FanOut | 1,256.71 ns | 2.806 ns | 4.200 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DispatchR_FanOut | 4,158.06 ns | 6.061 ns | 8.693 ns | 3.31 | 0.01 | 2 | 0.6485 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 90.42 ns | 0.170 ns | 0.244 ns | 1.00 | 0.00 | 1 | 0.0055 | - | 72 B | 1.00 |
| DispatchR_Throughput | 3,031.05 ns | 4.604 ns | 6.603 ns | 33.52 | 0.11 | 2 | 0.0038 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,289.45 ns | 6.003 ns | 8.986 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatorSG_FanOut | 2,031.01 ns | 3.608 ns | 5.289 ns | 1.58 | 0.01 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 84.97 ns | 0.376 ns | 0.551 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| MediatorSG_Throughput | 1,209.95 ns | 2.770 ns | 4.060 ns | 14.24 | 0.10 | 2 | 0.0038 | - | 72 B | 1.00 |

## Cold Start - All Libraries

> **Read the gap, not the total.** `Startup_ContainerOnly` builds the DI container and resolves the
> mediator. `Startup_WithFirstDispatch` does the same and then dispatches one request. Nearly all of
> either number is .NET runtime startup and DI container construction, which every library on this
> page pays alike. **The difference between the two rows is the part that belongs to the library.**
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean — and
> the first row executed also absorbs the machine's own file-cache warm-up, which inflates it and so
> understates the gap.

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Startup_ContainerOnly | 17.00 ms | 5.541 ms | 9.849 ms | 15.40 ms | 1.08 | 0.64 | 1 | 17.3 KB | 1.00 |
| DSoft_Startup_WithFirstDispatch | 19.92 ms | 0.135 ms | 0.240 ms | 19.92 ms | 1.26 | 0.16 | 2 | 17.37 KB | 1.00 |
| | | | | | | | | | |
| MediatR_Startup_ContainerOnly | 34.45 ms | 5.315 ms | 9.447 ms | 32.94 ms | 1.03 | 0.30 | 1 | 12.41 KB | 1.00 |
| MediatR_Startup_WithFirstDispatch | 34.58 ms | 0.204 ms | 0.363 ms | 34.62 ms | 1.03 | 0.11 | 2 | 15.22 KB | 1.23 |
| | | | | | | | | | |
| DispatchR_Startup_ContainerOnly | 15.46 ms | 5.314 ms | 9.445 ms | 13.93 ms | 1.08 | 0.67 | 1 | 14.66 KB | 1.00 |
| DispatchR_Startup_WithFirstDispatch | 18.07 ms | 0.098 ms | 0.174 ms | 18.02 ms | 1.27 | 0.16 | 2 | 17.04 KB | 1.16 |
| | | | | | | | | | |
| MediatorSG_Startup_ContainerOnly | 16.02 ms | 5.325 ms | 9.465 ms | 14.52 ms | 1.08 | 0.65 | 1 | 15.51 KB | 1.00 |
| MediatorSG_Startup_WithFirstDispatch | 23.30 ms | 0.196 ms | 0.349 ms | 23.34 ms | 1.57 | 0.20 | 2 | 150.35 KB | 9.70 |

## Realistic Pipeline - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall_WithPipeline | 669.4 ns | 2.47 ns | 3.62 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| DSoft_RealisticPipeline | 682.0 ns | 2.78 ns | 4.15 ns | 1.02 | 1 | 0.0191 | 254 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 668.0 ns | 3.56 ns | 5.22 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| MediatR_RealisticPipeline | 847.7 ns | 3.54 ns | 5.18 ns | 1.27 | 2 | 0.0782 | 1032 B | 3.81 |
| | | | | | | | | |
| DirectCall_WithPipeline | 671.6 ns | 1.54 ns | 2.31 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| DispatchR_RealisticPipeline | 693.8 ns | 2.21 ns | 3.31 ns | 1.03 | 2 | 0.0191 | 255 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 689.4 ns | 4.01 ns | 6.00 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| MediatorSG_RealisticPipeline | 732.4 ns | 3.14 ns | 4.60 ns | 1.06 | 2 | 0.0305 | 398 B | 1.47 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/<tfm>/results/`.
