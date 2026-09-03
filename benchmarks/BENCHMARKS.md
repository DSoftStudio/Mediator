# Benchmarks

Target framework: `net10.0`

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

> **Note:** Each library's benchmarks run in **isolated processes** (only that library active).
> The `All Libraries` sections below concatenate those isolated results for easy comparison.

## DSoft - Send (No Behaviors)

| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall | 6.825 ns | 0.0331 ns | 0.0310 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 6.844 ns | 0.0386 ns | 0.0342 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Behaviors)

| Method                | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.888 ns | 0.0256 ns | 0.0239 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 11.410 ns | 0.0271 ns | 0.0227 ns |  1.66 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 11.415 ns | 0.0411 ns | 0.0384 ns |  1.66 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Object)

| Method             | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  7.080 ns | 0.0275 ns | 0.0257 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 10.975 ns | 0.0192 ns | 0.0150 ns |  1.55 |    2 | 0.0073 |      96 B |        1.33 |

## DSoft - Publish

| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 3.686 ns | 0.0218 ns | 0.0204 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 4.316 ns | 0.0089 ns | 0.0083 ns |  1.17 |    2 |         - |          NA |

## DSoft - Publish (Object)

| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 4.198 ns | 0.0157 ns | 0.0147 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish_Object  | 5.981 ns | 0.0297 ns | 0.0264 ns |  1.42 |    2 |         - |          NA |

## DSoft - Stream

| Method        | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Stream  | 44.60 ns | 0.114 ns | 0.101 ns |  0.98 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream | 45.32 ns | 0.305 ns | 0.286 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |

## DSoft - Concurrency

| Method            | Categories | Mean        | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,293.35 ns | 3.446 ns | 3.224 ns |  0.99 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,309.54 ns | 3.664 ns | 3.248 ns |  1.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |      |        |        |           |             |
| Direct_Throughput | Throughput |    92.85 ns | 0.201 ns | 0.179 ns |  1.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   116.95 ns | 0.351 ns | 0.328 ns |  1.26 |    2 | 0.0055 |      - |      72 B |        1.00 |

## DSoft - Cold Start

| Method          | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|---------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DSoft_ColdStart | 1.984 μs | 0.0097 μs | 0.0081 μs |    1 | 0.8736 | 0.0267 |  11.17 KB |

## DSoft - Realistic Pipeline

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline | 674.5 ns | 3.97 ns | 3.72 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| DSoft_RealisticPipeline | 691.3 ns | 3.23 ns | 2.87 ns |  1.02 |    1 | 0.0191 |     254 B |        0.94 |

## DSoft - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall      |  7.113 ns | 0.0267 ns | 0.0250 ns |  1.00 |    2 | 0.0055 |      72 B |        1.00 |
| Send_0Behaviors |  6.830 ns | 0.0490 ns | 0.0458 ns |  0.96 |    1 | 0.0055 |      72 B |        1.00 |
| Send_1Behaviors | 11.464 ns | 0.0246 ns | 0.0218 ns |  1.61 |    3 | 0.0055 |      72 B |        1.00 |
| Send_2Behaviors | 11.411 ns | 0.0292 ns | 0.0273 ns |  1.60 |    3 | 0.0055 |      72 B |        1.00 |
| Send_3Behaviors | 11.229 ns | 0.0481 ns | 0.0402 ns |  1.58 |    3 | 0.0055 |      72 B |        1.00 |
| Send_5Behaviors | 12.138 ns | 0.0329 ns | 0.0307 ns |  1.71 |    4 | 0.0055 |      72 B |        1.00 |
| Send_8Behaviors | 12.182 ns | 0.0355 ns | 0.0315 ns |  1.71 |    4 | 0.0055 |      72 B |        1.00 |

## MediatR - Send (No Behaviors)

| Method       | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   |  8.862 ns | 0.0861 ns | 0.0805 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send | 41.550 ns | 0.1786 ns | 0.1671 ns |  4.69 |    0.05 |    2 | 0.0208 |     272 B |        1.89 |

## MediatR - Send (Behaviors)

| Method                  | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   8.847 ns | 0.0478 ns | 0.0424 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 108.240 ns | 0.2639 ns | 0.2469 ns | 12.23 |    0.06 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 152.104 ns | 0.5006 ns | 0.4683 ns | 17.19 |    0.09 |    3 | 0.0832 |    1088 B |        7.56 |

## MediatR - Send (Object)

| Method               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Send_Generic | 43.76 ns | 0.113 ns | 0.101 ns |  1.00 |    1 | 0.0208 |     272 B |        1.00 |
| MediatR_Send_Object  | 48.35 ns | 0.141 ns | 0.132 ns |  1.10 |    2 | 0.0281 |     368 B |        1.35 |

## MediatR - Publish

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish  |   3.120 ns | 0.0124 ns | 0.0116 ns |  1.00 |    0.01 |    1 |      - |         - |          NA |
| MediatR_Publish | 122.030 ns | 0.3538 ns | 0.2955 ns | 39.11 |    0.17 |    2 | 0.0587 |     768 B |          NA |

## MediatR - Publish (Object)

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 116.4 ns | 0.30 ns | 0.28 ns |  0.93 |    1 | 0.0587 |     768 B |        1.00 |
| MediatR_Publish_Generic | 124.8 ns | 0.34 ns | 0.32 ns |  1.00 |    2 | 0.0587 |     768 B |        1.00 |

## MediatR - Stream

| Method         | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  45.20 ns | 0.256 ns | 0.240 ns |  1.00 |    0.01 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 126.17 ns | 0.376 ns | 0.314 ns |  2.79 |    0.02 |    2 | 0.0477 |     624 B |        2.69 |

## MediatR - Concurrency

| Method             | Categories | Mean       | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |-----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,323.7 ns | 16.83 ns | 15.74 ns |  1.00 |    0.02 |    1 | 0.6523 | 0.0172 |   8.34 KB |        1.00 |
| MediatR_FanOut     | FanOut     | 4,648.6 ns | 57.30 ns | 53.60 ns |  3.51 |    0.06 |    2 | 1.6251 | 0.0381 |  20.84 KB |        2.50 |
|                    |            |            |          |          |       |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |   387.3 ns |  3.63 ns |  3.22 ns |  1.00 |    0.01 |    1 | 0.5560 |      - |    7.1 KB |        1.00 |
| MediatR_Throughput | Throughput | 3,664.3 ns |  8.83 ns |  7.37 ns |  9.46 |    0.08 |    2 | 1.5335 |      - |   19.6 KB |        2.76 |

## MediatR - Cold Start

| Method            | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|------------------ |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatR_ColdStart | 3.247 μs | 0.0175 μs | 0.0164 μs |    1 | 0.9804 | 0.0305 |  12.55 KB |

## MediatR - Realistic Pipeline

| Method                    | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   | 680.5 ns | 5.39 ns | 5.04 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| MediatR_RealisticPipeline | 833.8 ns | 6.03 ns | 5.64 ns |  1.23 |    2 | 0.0782 |    1032 B |        3.81 |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall     |  5.484 ns | 0.0248 ns | 0.0232 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send | 33.391 ns | 0.1195 ns | 0.1059 ns |  6.09 |    0.03 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  5.519 ns | 0.0578 ns | 0.0540 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 52.649 ns | 0.2122 ns | 0.1985 ns |  9.54 |    0.10 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 53.122 ns | 0.1305 ns | 0.1221 ns |  9.63 |    0.09 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Publish

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  3.265 ns | 0.0220 ns | 0.0205 ns |  1.00 |    0.01 |    1 |         - |          NA |
| DispatchR_Publish | 35.055 ns | 0.0390 ns | 0.0346 ns | 10.74 |    0.07 |    2 |         - |          NA |

## DispatchR - Publish (Object)

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  35.98 ns | 0.082 ns | 0.072 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 217.59 ns | 0.851 ns | 0.796 ns |  6.05 |    0.02 |    2 | 0.0196 |     256 B |          NA |

## DispatchR - Stream

| Method           | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 45.15 ns | 0.402 ns | 0.376 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 68.26 ns | 0.316 ns | 0.280 ns |  1.51 |    2 | 0.0176 |     232 B |        1.00 |

## DispatchR - Concurrency

| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,301.79 ns |  4.147 ns |  3.879 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,711.34 ns | 15.207 ns | 14.225 ns |  2.85 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    91.67 ns |  0.339 ns |  0.265 ns |  1.00 |    0.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,012.57 ns |  4.133 ns |  3.866 ns | 32.86 |    0.10 |    2 | 0.0038 |      - |      72 B |        1.00 |

## DispatchR - Cold Start

| Method              | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|-------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DispatchR_ColdStart | 1.857 μs | 0.0081 μs | 0.0075 μs |    1 | 0.6866 | 0.0191 |   8.77 KB |

## DispatchR - Realistic Pipeline

| Method                      | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline     | 671.7 ns | 3.32 ns | 2.77 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| DispatchR_RealisticPipeline | 694.4 ns | 2.83 ns | 2.64 ns |  1.03 |    2 | 0.0191 |     255 B |        0.94 |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall      |  5.797 ns | 0.0172 ns | 0.0152 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 12.740 ns | 0.0477 ns | 0.0423 ns |  2.20 |    2 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  5.495 ns | 0.0293 ns | 0.0245 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 27.613 ns | 0.1095 ns | 0.0971 ns |  5.03 |    0.03 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 36.567 ns | 0.0471 ns | 0.0368 ns |  6.65 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Object)

| Method                  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Send_Generic | 13.25 ns | 0.068 ns | 0.063 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_Object  | 15.02 ns | 0.051 ns | 0.042 ns |  1.13 |    2 | 0.0073 |      96 B |        1.33 |

## Mediator (Source Gen) - Publish

| Method             | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish     |  3.277 ns | 0.0145 ns | 0.0136 ns |  1.00 |    1 |         - |          NA |
| MediatorSG_Publish | 10.797 ns | 0.0198 ns | 0.0185 ns |  3.30 |    2 |         - |          NA |

## Mediator (Source Gen) - Publish (Object)

| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  8.348 ns | 0.0435 ns | 0.0407 ns |  0.76 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 11.052 ns | 0.0379 ns | 0.0355 ns |  1.00 |    2 |         - |          NA |

## Mediator (Source Gen) - Stream

| Method            | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Stream | 44.92 ns | 0.132 ns | 0.123 ns |  0.99 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream     | 45.16 ns | 0.238 ns | 0.211 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |

## Mediator (Source Gen) - Concurrency

| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,302.92 ns | 3.293 ns | 2.749 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,603.82 ns | 5.457 ns | 4.838 ns |  1.23 |    0.00 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    93.61 ns | 0.437 ns | 0.409 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput |   892.03 ns | 0.689 ns | 0.611 ns |  9.53 |    0.04 |    2 | 0.0048 |      - |      72 B |        1.00 |

## Mediator (Source Gen) - Cold Start

| Method               | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 9.324 μs | 0.0516 μs | 0.0483 μs |    1 | 2.5330 | 0.1984 |   32.4 KB |

## Mediator (Source Gen) - Realistic Pipeline

| Method                       | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      | 665.0 ns | 3.26 ns | 3.05 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| MediatorSG_RealisticPipeline | 729.9 ns | 4.39 ns | 4.10 ns |  1.10 |    2 | 0.0305 |     398 B |        1.47 |

## Send - All Libraries (No Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 6.825 ns | 0.0331 ns | 0.0310 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send | 6.844 ns | 0.0386 ns | 0.0342 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 8.862 ns | 0.0861 ns | 0.0805 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send | 41.550 ns | 0.1786 ns | 0.1671 ns | 4.69 | 0.05 | 2 | 0.0208 | 272 B | 1.89 |
| | | | | | | | | | |
| DirectCall | 5.484 ns | 0.0248 ns | 0.0232 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send | 33.391 ns | 0.1195 ns | 0.1059 ns | 6.09 | 0.03 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 5.797 ns | 0.0172 ns | 0.0152 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send | 12.740 ns | 0.0477 ns | 0.0423 ns | 2.20 | - | 2 | 0.0055 | 72 B | 1.00 |

## Send - All Libraries (Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 6.888 ns | 0.0256 ns | 0.0239 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_5Behaviors | 11.410 ns | 0.0271 ns | 0.0227 ns | 1.66 | - | 2 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_3Behaviors | 11.415 ns | 0.0411 ns | 0.0384 ns | 1.66 | - | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 8.847 ns | 0.0478 ns | 0.0424 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send_3Behaviors | 108.240 ns | 0.2639 ns | 0.2469 ns | 12.23 | 0.06 | 2 | 0.0612 | 800 B | 5.56 |
| MediatR_Send_5Behaviors | 152.104 ns | 0.5006 ns | 0.4683 ns | 17.19 | 0.09 | 3 | 0.0832 | 1088 B | 7.56 |
| | | | | | | | | | |
| DirectCall | 5.519 ns | 0.0578 ns | 0.0540 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_5Behaviors | 52.649 ns | 0.2122 ns | 0.1985 ns | 9.54 | 0.10 | 2 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_3Behaviors | 53.122 ns | 0.1305 ns | 0.1221 ns | 9.63 | 0.09 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 5.495 ns | 0.0293 ns | 0.0245 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_3Behaviors | 27.613 ns | 0.1095 ns | 0.0971 ns | 5.03 | 0.03 | 2 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_5Behaviors | 36.567 ns | 0.0471 ns | 0.0368 ns | 6.65 | 0.03 | 3 | 0.0055 | 72 B | 1.00 |

## Send (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send_Generic | 7.080 ns | 0.0275 ns | 0.0257 ns | 1.00 | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_Object | 10.975 ns | 0.0192 ns | 0.0150 ns | 1.55 | 2 | 0.0073 | 96 B | 1.33 |
| | | | | | | | | |
| MediatR_Send_Generic | 43.76 ns | 0.113 ns | 0.101 ns | 1.00 | 1 | 0.0208 | 272 B | 1.00 |
| MediatR_Send_Object | 48.35 ns | 0.141 ns | 0.132 ns | 1.10 | 2 | 0.0281 | 368 B | 1.35 |
| | | | | | | | | |
| MediatorSG_Send_Generic | 13.25 ns | 0.068 ns | 0.063 ns | 1.00 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_Object | 15.02 ns | 0.051 ns | 0.042 ns | 1.13 | 2 | 0.0073 | 96 B | 1.33 |

## Publish - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Publish | 3.686 ns | 0.0218 ns | 0.0204 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish | 4.316 ns | 0.0089 ns | 0.0083 ns | 1.17 | - | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.120 ns | 0.0124 ns | 0.0116 ns | 1.00 | 0.01 | 1 | - | - | NA |
| MediatR_Publish | 122.030 ns | 0.3538 ns | 0.2955 ns | 39.11 | 0.17 | 2 | 0.0587 | 768 B | NA |
| | | | | | | | | | |
| Direct_Publish | 3.265 ns | 0.0220 ns | 0.0205 ns | 1.00 | 0.01 | 1 | - | - | NA |
| DispatchR_Publish | 35.055 ns | 0.0390 ns | 0.0346 ns | 10.74 | 0.07 | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.277 ns | 0.0145 ns | 0.0136 ns | 1.00 | - | 1 | - | - | NA |
| MediatorSG_Publish | 10.797 ns | 0.0198 ns | 0.0185 ns | 3.30 | - | 2 | - | - | NA |

## Publish (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Publish_Generic | 4.198 ns | 0.0157 ns | 0.0147 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish_Object | 5.981 ns | 0.0297 ns | 0.0264 ns | 1.42 | - | 2 | - | - | NA |
| | | | | | | | | | |
| MediatR_Publish_Object | 116.4 ns | 0.30 ns | 0.28 ns | 0.93 | - | 1 | 0.0587 | 768 B | 1.00 |
| MediatR_Publish_Generic | 124.8 ns | 0.34 ns | 0.32 ns | 1.00 | - | 2 | 0.0587 | 768 B | 1.00 |
| | | | | | | | | | |
| DispatchR_Publish_Generic | 35.98 ns | 0.082 ns | 0.072 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish_Object | 217.59 ns | 0.851 ns | 0.796 ns | 6.05 | 0.02 | 2 | 0.0196 | 256 B | NA |
| | | | | | | | | | |
| MediatorSG_Publish_Object | 8.348 ns | 0.0435 ns | 0.0407 ns | 0.76 | - | 1 | - | - | NA |
| MediatorSG_Publish_Generic | 11.052 ns | 0.0379 ns | 0.0355 ns | 1.00 | - | 2 | - | - | NA |

## Stream - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Stream | 44.60 ns | 0.114 ns | 0.101 ns | 0.98 | - | 1 | 0.0177 | 232 B | 1.00 |
| Direct_Stream | 45.32 ns | 0.305 ns | 0.286 ns | 1.00 | - | 1 | 0.0177 | 232 B | 1.00 |
| | | | | | | | | | |
| Direct_Stream | 45.20 ns | 0.256 ns | 0.240 ns | 1.00 | 0.01 | 1 | 0.0177 | 232 B | 1.00 |
| MediatR_Stream | 126.17 ns | 0.376 ns | 0.314 ns | 2.79 | 0.02 | 2 | 0.0477 | 624 B | 2.69 |
| | | | | | | | | | |
| Direct_Stream | 45.15 ns | 0.402 ns | 0.376 ns | 1.00 | - | 1 | 0.0177 | 232 B | 1.00 |
| DispatchR_Stream | 68.26 ns | 0.316 ns | 0.280 ns | 1.51 | - | 2 | 0.0176 | 232 B | 1.00 |
| | | | | | | | | | |
| MediatorSG_Stream | 44.92 ns | 0.132 ns | 0.123 ns | 0.99 | - | 1 | 0.0177 | 232 B | 1.00 |
| Direct_Stream | 45.16 ns | 0.238 ns | 0.211 ns | 1.00 | - | 1 | 0.0177 | 232 B | 1.00 |

## Concurrency - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_FanOut | 1,293.35 ns | 3.446 ns | 3.224 ns | 0.99 | - | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_FanOut | 1,309.54 ns | 3.664 ns | 3.248 ns | 1.00 | - | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 92.85 ns | 0.201 ns | 0.179 ns | 1.00 | - | 1 | 0.0055 | - | 72 B | 1.00 |
| DSoft_Throughput | 116.95 ns | 0.351 ns | 0.328 ns | 1.26 | - | 2 | 0.0055 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,323.7 ns | 16.83 ns | 15.74 ns | 1.00 | 0.02 | 1 | 0.6523 | 0.0172 | 8.34 KB | 1.00 |
| MediatR_FanOut | 4,648.6 ns | 57.30 ns | 53.60 ns | 3.51 | 0.06 | 2 | 1.6251 | 0.0381 | 20.84 KB | 2.50 |
| Direct_Throughput | 387.3 ns | 3.63 ns | 3.22 ns | 1.00 | 0.01 | 1 | 0.5560 | - | 7.1 KB | 1.00 |
| MediatR_Throughput | 3,664.3 ns | 8.83 ns | 7.37 ns | 9.46 | 0.08 | 2 | 1.5335 | - | 19.6 KB | 2.76 |
| | | | | | | | | | | |
| Direct_FanOut | 1,301.79 ns | 4.147 ns | 3.879 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DispatchR_FanOut | 3,711.34 ns | 15.207 ns | 14.225 ns | 2.85 | 0.01 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 91.67 ns | 0.339 ns | 0.265 ns | 1.00 | 0.00 | 1 | 0.0055 | - | 72 B | 1.00 |
| DispatchR_Throughput | 3,012.57 ns | 4.133 ns | 3.866 ns | 32.86 | 0.10 | 2 | 0.0038 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,302.92 ns | 3.293 ns | 2.749 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatorSG_FanOut | 1,603.82 ns | 5.457 ns | 4.838 ns | 1.23 | 0.00 | 2 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 93.61 ns | 0.437 ns | 0.409 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| MediatorSG_Throughput | 892.03 ns | 0.689 ns | 0.611 ns | 9.53 | 0.04 | 2 | 0.0048 | - | 72 B | 1.00 |

## Cold Start - All Libraries

| Method | Mean | Error | StdDev | Rank | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_ColdStart | 1.984 μs | 0.0097 μs | 0.0081 μs | 1 | 0.8736 | 0.0267 | 11.17 KB |
| | | | | | | | |
| MediatR_ColdStart | 3.247 μs | 0.0175 μs | 0.0164 μs | 1 | 0.9804 | 0.0305 | 12.55 KB |
| | | | | | | | |
| DispatchR_ColdStart | 1.857 μs | 0.0081 μs | 0.0075 μs | 1 | 0.6866 | 0.0191 | 8.77 KB |
| | | | | | | | |
| MediatorSG_ColdStart | 9.324 μs | 0.0516 μs | 0.0483 μs | 1 | 2.5330 | 0.1984 | 32.4 KB |

## Realistic Pipeline - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall_WithPipeline | 674.5 ns | 3.97 ns | 3.72 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| DSoft_RealisticPipeline | 691.3 ns | 3.23 ns | 2.87 ns | 1.02 | 1 | 0.0191 | 254 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 680.5 ns | 5.39 ns | 5.04 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| MediatR_RealisticPipeline | 833.8 ns | 6.03 ns | 5.64 ns | 1.23 | 2 | 0.0782 | 1032 B | 3.81 |
| | | | | | | | | |
| DirectCall_WithPipeline | 671.7 ns | 3.32 ns | 2.77 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| DispatchR_RealisticPipeline | 694.4 ns | 2.83 ns | 2.64 ns | 1.03 | 2 | 0.0191 | 255 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 665.0 ns | 3.26 ns | 3.05 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| MediatorSG_RealisticPipeline | 729.9 ns | 4.39 ns | 4.10 ns | 1.10 | 2 | 0.0305 | 398 B | 1.47 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/<tfm>/results/`.
