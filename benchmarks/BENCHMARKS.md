# Benchmarks

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8039/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
```

> **Note:** Each library's benchmarks run in **isolated processes** (only that library active).
> The `All Libraries` sections below concatenate those isolated results for easy comparison.

## DSoft - Send (No Behaviors)

| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall | 6.996 ns | 0.0424 ns | 0.0397 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send | 7.245 ns | 0.0654 ns | 0.0612 ns |  1.04 |    2 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Behaviors)

| Method                | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DirectCall            |  6.745 ns | 0.0422 ns | 0.0394 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_3Behaviors | 13.820 ns | 0.0389 ns | 0.0364 ns |  2.05 |    2 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_5Behaviors | 15.635 ns | 0.0178 ns | 0.0158 ns |  2.32 |    3 | 0.0055 |      72 B |        1.00 |

## DSoft - Send (Object)

| Method             | Mean      | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic |  7.290 ns | 0.0226 ns | 0.0200 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| DSoft_Send_Object  | 11.346 ns | 0.0219 ns | 0.0194 ns |  1.56 |    2 | 0.0073 |      96 B |        1.33 |

## DSoft - Publish

| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 3.788 ns | 0.0115 ns | 0.0108 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 4.501 ns | 0.0168 ns | 0.0157 ns |  1.19 |    2 |         - |          NA |

## DSoft - Publish (Object)

| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 4.568 ns | 0.0289 ns | 0.0270 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish_Object  | 6.561 ns | 0.0425 ns | 0.0397 ns |  1.44 |    2 |         - |          NA |

## DSoft - Stream

| Method        | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream | 44.77 ns | 0.290 ns | 0.271 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DSoft_Stream  | 45.52 ns | 0.240 ns | 0.225 ns |  1.02 |    1 | 0.0177 |     232 B |        1.00 |

## DSoft - Concurrency

| Method            | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut     | FanOut     | 1,310.48 ns | 11.886 ns | 11.118 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DSoft_FanOut      | FanOut     | 1,368.67 ns | 26.464 ns | 25.991 ns |  1.04 |    0.02 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    92.01 ns |  0.266 ns |  0.249 ns |  1.00 |    0.00 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DSoft_Throughput  | Throughput |   192.87 ns |  0.473 ns |  0.395 ns |  2.10 |    0.01 |    2 | 0.0055 |      - |      72 B |        1.00 |

## DSoft - Cold Start

| Method          | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|---------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DSoft_ColdStart | 1.622 μs | 0.0081 μs | 0.0076 μs |    1 | 0.7229 | 0.0401 |   9.24 KB |

## DSoft - Realistic Pipeline

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DSoft_RealisticPipeline | 666.9 ns | 4.61 ns | 4.31 ns |  0.99 |    1 | 0.0191 |     255 B |        0.94 |
| DirectCall_WithPipeline | 674.1 ns | 3.60 ns | 3.37 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |

## MediatR - Send (No Behaviors)

| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   | 10.20 ns | 0.054 ns | 0.051 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send | 41.33 ns | 0.080 ns | 0.071 ns |  4.05 |    0.02 |    2 | 0.0208 |     272 B |        1.89 |

## MediatR - Send (Behaviors)

| Method                  | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |  10.17 ns | 0.080 ns | 0.075 ns |  1.00 |    0.01 |    1 | 0.0110 |     144 B |        1.00 |
| MediatR_Send_3Behaviors | 108.13 ns | 0.301 ns | 0.267 ns | 10.63 |    0.08 |    2 | 0.0612 |     800 B |        5.56 |
| MediatR_Send_5Behaviors | 153.13 ns | 0.662 ns | 0.619 ns | 15.05 |    0.12 |    3 | 0.0832 |    1088 B |        7.56 |

## MediatR - Send (Object)

| Method               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Send_Generic | 42.05 ns | 0.089 ns | 0.083 ns |  1.00 |    1 | 0.0208 |     272 B |        1.00 |
| MediatR_Send_Object  | 47.90 ns | 0.284 ns | 0.266 ns |  1.14 |    2 | 0.0281 |     368 B |        1.35 |

## MediatR - Publish

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish  |   3.773 ns | 0.0180 ns | 0.0160 ns |  1.00 |    0.01 |    1 |      - |         - |          NA |
| MediatR_Publish | 123.381 ns | 0.5452 ns | 0.5100 ns | 32.70 |    0.19 |    2 | 0.0587 |     768 B |          NA |

## MediatR - Publish (Object)

| Method                  | Mean     | Error   | StdDev  | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|--------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 126.5 ns | 2.49 ns | 2.33 ns |  1.00 |    0.02 |    1 | 0.0587 |     768 B |        1.00 |
| MediatR_Publish_Generic | 126.9 ns | 0.71 ns | 0.66 ns |  1.00 |    0.01 |    1 | 0.0587 |     768 B |        1.00 |

## MediatR - Stream

| Method         | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  44.57 ns | 0.210 ns | 0.186 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| MediatR_Stream | 122.87 ns | 0.375 ns | 0.351 ns |  2.76 |    2 | 0.0477 |     624 B |        2.69 |

## MediatR - Concurrency

| Method             | Categories | Mean       | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |-----------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,279.2 ns |  6.02 ns |  5.63 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |   8.34 KB |        1.00 |
| MediatR_FanOut     | FanOut     | 4,531.2 ns | 12.79 ns | 10.68 ns |  3.54 |    0.02 |    2 | 1.6251 | 0.0381 |  20.84 KB |        2.50 |
|                    |            |            |          |          |       |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |   381.7 ns |  3.01 ns |  2.67 ns |  1.00 |    0.01 |    1 | 0.5560 |      - |    7.1 KB |        1.00 |
| MediatR_Throughput | Throughput | 3,629.7 ns | 11.80 ns | 11.04 ns |  9.51 |    0.07 |    2 | 1.5335 |      - |   19.6 KB |        2.76 |

## MediatR - Cold Start

| Method            | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|------------------ |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatR_ColdStart | 3.241 μs | 0.0186 μs | 0.0174 μs |    1 | 0.9766 | 0.0305 |  12.51 KB |

## MediatR - Realistic Pipeline

| Method                    | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   | 713.9 ns | 3.65 ns | 3.41 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatR_RealisticPipeline | 857.1 ns | 2.38 ns | 2.23 ns |  1.20 |    2 | 0.0782 |    1032 B |        3.82 |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall     |  6.644 ns | 0.0317 ns | 0.0297 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send | 33.413 ns | 0.0500 ns | 0.0418 ns |  5.03 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                |  7.426 ns | 0.0466 ns | 0.0413 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_3Behaviors | 53.161 ns | 0.1991 ns | 0.1765 ns |  7.16 |    0.04 |    2 | 0.0055 |      72 B |        1.00 |
| DispatchR_Send_5Behaviors | 54.117 ns | 0.2459 ns | 0.2179 ns |  7.29 |    0.05 |    2 | 0.0055 |      72 B |        1.00 |

## DispatchR - Publish

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  3.884 ns | 0.0429 ns | 0.0402 ns |  1.00 |    0.01 |    1 |         - |          NA |
| DispatchR_Publish | 35.746 ns | 0.0679 ns | 0.0602 ns |  9.21 |    0.09 |    2 |         - |          NA |

## DispatchR - Publish (Object)

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  35.10 ns | 0.179 ns | 0.159 ns |  1.00 |    0.01 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 228.36 ns | 0.831 ns | 0.778 ns |  6.51 |    0.04 |    2 | 0.0196 |     256 B |          NA |

## DispatchR - Stream

| Method           | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 46.84 ns | 0.204 ns | 0.170 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |
| DispatchR_Stream | 68.08 ns | 0.120 ns | 0.100 ns |  1.45 |    2 | 0.0176 |     232 B |        1.00 |

## DispatchR - Concurrency

| Method               | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,322.88 ns | 3.737 ns | 3.312 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,721.47 ns | 7.646 ns | 7.152 ns |  2.81 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    85.49 ns | 0.821 ns | 0.768 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| DispatchR_Throughput | Throughput | 3,031.98 ns | 5.230 ns | 4.893 ns | 35.47 |    0.31 |    2 | 0.0038 |      - |      72 B |        1.00 |

## DispatchR - Cold Start

| Method              | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|-------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DispatchR_ColdStart | 1.882 μs | 0.0096 μs | 0.0090 μs |    1 | 0.6866 | 0.0191 |   8.77 KB |

## DispatchR - Realistic Pipeline

| Method                      | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline     | 661.2 ns | 5.18 ns | 4.85 ns |  1.00 |    1 | 0.0200 |     271 B |        1.00 |
| DispatchR_RealisticPipeline | 666.7 ns | 2.72 ns | 2.54 ns |  1.01 |    1 | 0.0191 |     255 B |        0.94 |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall      |  7.042 ns | 0.0735 ns | 0.0688 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send | 12.155 ns | 0.0638 ns | 0.0596 ns |  1.73 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall                 |  7.058 ns | 0.0393 ns | 0.0368 ns |  1.00 |    0.01 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_3Behaviors | 27.965 ns | 0.0437 ns | 0.0408 ns |  3.96 |    0.02 |    2 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_5Behaviors | 36.753 ns | 0.0843 ns | 0.0789 ns |  5.21 |    0.03 |    3 | 0.0055 |      72 B |        1.00 |

## Mediator (Source Gen) - Send (Object)

| Method                  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Send_Generic | 12.16 ns | 0.034 ns | 0.031 ns |  1.00 |    1 | 0.0055 |      72 B |        1.00 |
| MediatorSG_Send_Object  | 15.72 ns | 0.068 ns | 0.060 ns |  1.29 |    2 | 0.0073 |      96 B |        1.33 |

## Mediator (Source Gen) - Publish

| Method             | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish     |  4.150 ns | 0.0486 ns | 0.0454 ns |  1.00 |    0.01 |    1 |         - |          NA |
| MediatorSG_Publish | 10.602 ns | 0.0212 ns | 0.0198 ns |  2.55 |    0.03 |    2 |         - |          NA |

## Mediator (Source Gen) - Publish (Object)

| Method                     | Mean      | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  |  8.315 ns | 0.0130 ns | 0.0122 ns |  0.77 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 10.790 ns | 0.0444 ns | 0.0415 ns |  1.00 |    2 |         - |          NA |

## Mediator (Source Gen) - Stream

| Method            | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Stream | 44.72 ns | 0.187 ns | 0.166 ns |  0.99 |    1 | 0.0177 |     232 B |        1.00 |
| Direct_Stream     | 45.18 ns | 0.240 ns | 0.224 ns |  1.00 |    1 | 0.0177 |     232 B |        1.00 |

## Mediator (Source Gen) - Concurrency

| Method                | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,317.43 ns |  6.104 ns |  5.710 ns |  1.00 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,725.83 ns | 11.132 ns | 10.413 ns |  1.31 |    0.01 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                       |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    94.30 ns |  0.499 ns |  0.416 ns |  1.00 |    0.01 |    1 | 0.0055 |      - |      72 B |        1.00 |
| MediatorSG_Throughput | Throughput |   849.36 ns |  1.284 ns |  1.201 ns |  9.01 |    0.04 |    2 | 0.0048 |      - |      72 B |        1.00 |

## Mediator (Source Gen) - Cold Start

| Method               | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 9.911 μs | 0.0582 μs | 0.0544 μs |    1 | 2.5330 | 0.1984 |   32.4 KB |

## Mediator (Source Gen) - Realistic Pipeline

| Method                       | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      | 679.2 ns | 4.57 ns | 4.27 ns |  1.00 |    1 | 0.0200 |     270 B |        1.00 |
| MediatorSG_RealisticPipeline | 718.0 ns | 8.25 ns | 7.72 ns |  1.06 |    2 | 0.0296 |     397 B |        1.47 |

## Send - All Libraries (No Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 6.996 ns | 0.0424 ns | 0.0397 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send | 7.245 ns | 0.0654 ns | 0.0612 ns | 1.04 | - | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 10.20 ns | 0.054 ns | 0.051 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send | 41.33 ns | 0.080 ns | 0.071 ns | 4.05 | 0.02 | 2 | 0.0208 | 272 B | 1.89 |
| | | | | | | | | | |
| DirectCall | 6.644 ns | 0.0317 ns | 0.0297 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send | 33.413 ns | 0.0500 ns | 0.0418 ns | 5.03 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 7.042 ns | 0.0735 ns | 0.0688 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send | 12.155 ns | 0.0638 ns | 0.0596 ns | 1.73 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |

## Send - All Libraries (Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 6.745 ns | 0.0422 ns | 0.0394 ns | 1.00 | - | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_3Behaviors | 13.820 ns | 0.0389 ns | 0.0364 ns | 2.05 | - | 2 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_5Behaviors | 15.635 ns | 0.0178 ns | 0.0158 ns | 2.32 | - | 3 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 10.17 ns | 0.080 ns | 0.075 ns | 1.00 | 0.01 | 1 | 0.0110 | 144 B | 1.00 |
| MediatR_Send_3Behaviors | 108.13 ns | 0.301 ns | 0.267 ns | 10.63 | 0.08 | 2 | 0.0612 | 800 B | 5.56 |
| MediatR_Send_5Behaviors | 153.13 ns | 0.662 ns | 0.619 ns | 15.05 | 0.12 | 3 | 0.0832 | 1088 B | 7.56 |
| | | | | | | | | | |
| DirectCall | 7.426 ns | 0.0466 ns | 0.0413 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_3Behaviors | 53.161 ns | 0.1991 ns | 0.1765 ns | 7.16 | 0.04 | 2 | 0.0055 | 72 B | 1.00 |
| DispatchR_Send_5Behaviors | 54.117 ns | 0.2459 ns | 0.2179 ns | 7.29 | 0.05 | 2 | 0.0055 | 72 B | 1.00 |
| | | | | | | | | | |
| DirectCall | 7.058 ns | 0.0393 ns | 0.0368 ns | 1.00 | 0.01 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_3Behaviors | 27.965 ns | 0.0437 ns | 0.0408 ns | 3.96 | 0.02 | 2 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_5Behaviors | 36.753 ns | 0.0843 ns | 0.0789 ns | 5.21 | 0.03 | 3 | 0.0055 | 72 B | 1.00 |

## Send (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send_Generic | 7.290 ns | 0.0226 ns | 0.0200 ns | 1.00 | 1 | 0.0055 | 72 B | 1.00 |
| DSoft_Send_Object | 11.346 ns | 0.0219 ns | 0.0194 ns | 1.56 | 2 | 0.0073 | 96 B | 1.33 |
| | | | | | | | | |
| MediatR_Send_Generic | 42.05 ns | 0.089 ns | 0.083 ns | 1.00 | 1 | 0.0208 | 272 B | 1.00 |
| MediatR_Send_Object | 47.90 ns | 0.284 ns | 0.266 ns | 1.14 | 2 | 0.0281 | 368 B | 1.35 |
| | | | | | | | | |
| MediatorSG_Send_Generic | 12.16 ns | 0.034 ns | 0.031 ns | 1.00 | 1 | 0.0055 | 72 B | 1.00 |
| MediatorSG_Send_Object | 15.72 ns | 0.068 ns | 0.060 ns | 1.29 | 2 | 0.0073 | 96 B | 1.33 |

## Publish - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Publish | 3.788 ns | 0.0115 ns | 0.0108 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish | 4.501 ns | 0.0168 ns | 0.0157 ns | 1.19 | - | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 3.773 ns | 0.0180 ns | 0.0160 ns | 1.00 | 0.01 | 1 | - | - | NA |
| MediatR_Publish | 123.381 ns | 0.5452 ns | 0.5100 ns | 32.70 | 0.19 | 2 | 0.0587 | 768 B | NA |
| | | | | | | | | | |
| Direct_Publish | 3.884 ns | 0.0429 ns | 0.0402 ns | 1.00 | 0.01 | 1 | - | - | NA |
| DispatchR_Publish | 35.746 ns | 0.0679 ns | 0.0602 ns | 9.21 | 0.09 | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 4.150 ns | 0.0486 ns | 0.0454 ns | 1.00 | 0.01 | 1 | - | - | NA |
| MediatorSG_Publish | 10.602 ns | 0.0212 ns | 0.0198 ns | 2.55 | 0.03 | 2 | - | - | NA |

## Publish (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Publish_Generic | 4.568 ns | 0.0289 ns | 0.0270 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish_Object | 6.561 ns | 0.0425 ns | 0.0397 ns | 1.44 | - | 2 | - | - | NA |
| | | | | | | | | | |
| MediatR_Publish_Object | 126.5 ns | 2.49 ns | 2.33 ns | 1.00 | 0.02 | 1 | 0.0587 | 768 B | 1.00 |
| MediatR_Publish_Generic | 126.9 ns | 0.71 ns | 0.66 ns | 1.00 | 0.01 | 1 | 0.0587 | 768 B | 1.00 |
| | | | | | | | | | |
| DispatchR_Publish_Generic | 35.10 ns | 0.179 ns | 0.159 ns | 1.00 | 0.01 | 1 | - | - | NA |
| DispatchR_Publish_Object | 228.36 ns | 0.831 ns | 0.778 ns | 6.51 | 0.04 | 2 | 0.0196 | 256 B | NA |
| | | | | | | | | | |
| MediatorSG_Publish_Object | 8.315 ns | 0.0130 ns | 0.0122 ns | 0.77 | - | 1 | - | - | NA |
| MediatorSG_Publish_Generic | 10.790 ns | 0.0444 ns | 0.0415 ns | 1.00 | - | 2 | - | - | NA |

## Stream - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Stream | 44.77 ns | 0.290 ns | 0.271 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| DSoft_Stream | 45.52 ns | 0.240 ns | 0.225 ns | 1.02 | 1 | 0.0177 | 232 B | 1.00 |
| | | | | | | | | |
| Direct_Stream | 44.57 ns | 0.210 ns | 0.186 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| MediatR_Stream | 122.87 ns | 0.375 ns | 0.351 ns | 2.76 | 2 | 0.0477 | 624 B | 2.69 |
| | | | | | | | | |
| Direct_Stream | 46.84 ns | 0.204 ns | 0.170 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |
| DispatchR_Stream | 68.08 ns | 0.120 ns | 0.100 ns | 1.45 | 2 | 0.0176 | 232 B | 1.00 |
| | | | | | | | | |
| MediatorSG_Stream | 44.72 ns | 0.187 ns | 0.166 ns | 0.99 | 1 | 0.0177 | 232 B | 1.00 |
| Direct_Stream | 45.18 ns | 0.240 ns | 0.224 ns | 1.00 | 1 | 0.0177 | 232 B | 1.00 |

## Concurrency - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_FanOut | 1,310.48 ns | 11.886 ns | 11.118 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DSoft_FanOut | 1,368.67 ns | 26.464 ns | 25.991 ns | 1.04 | 0.02 | 2 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 92.01 ns | 0.266 ns | 0.249 ns | 1.00 | 0.00 | 1 | 0.0055 | - | 72 B | 1.00 |
| DSoft_Throughput | 192.87 ns | 0.473 ns | 0.395 ns | 2.10 | 0.01 | 2 | 0.0055 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,279.2 ns | 6.02 ns | 5.63 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8.34 KB | 1.00 |
| MediatR_FanOut | 4,531.2 ns | 12.79 ns | 10.68 ns | 3.54 | 0.02 | 2 | 1.6251 | 0.0381 | 20.84 KB | 2.50 |
| Direct_Throughput | 381.7 ns | 3.01 ns | 2.67 ns | 1.00 | 0.01 | 1 | 0.5560 | - | 7.1 KB | 1.00 |
| MediatR_Throughput | 3,629.7 ns | 11.80 ns | 11.04 ns | 9.51 | 0.07 | 2 | 1.5335 | - | 19.6 KB | 2.76 |
| | | | | | | | | | | |
| Direct_FanOut | 1,322.88 ns | 3.737 ns | 3.312 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DispatchR_FanOut | 3,721.47 ns | 7.646 ns | 7.152 ns | 2.81 | 0.01 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 85.49 ns | 0.821 ns | 0.768 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| DispatchR_Throughput | 3,031.98 ns | 5.230 ns | 4.893 ns | 35.47 | 0.31 | 2 | 0.0038 | - | 72 B | 1.00 |
| | | | | | | | | | | |
| Direct_FanOut | 1,317.43 ns | 6.104 ns | 5.710 ns | 1.00 | 0.01 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatorSG_FanOut | 1,725.83 ns | 11.132 ns | 10.413 ns | 1.31 | 0.01 | 2 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 94.30 ns | 0.499 ns | 0.416 ns | 1.00 | 0.01 | 1 | 0.0055 | - | 72 B | 1.00 |
| MediatorSG_Throughput | 849.36 ns | 1.284 ns | 1.201 ns | 9.01 | 0.04 | 2 | 0.0048 | - | 72 B | 1.00 |

## Cold Start - All Libraries

| Method | Mean | Error | StdDev | Rank | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_ColdStart | 1.622 μs | 0.0081 μs | 0.0076 μs | 1 | 0.7229 | 0.0401 | 9.24 KB |
| | | | | | | | |
| MediatR_ColdStart | 3.241 μs | 0.0186 μs | 0.0174 μs | 1 | 0.9766 | 0.0305 | 12.51 KB |
| | | | | | | | |
| DispatchR_ColdStart | 1.882 μs | 0.0096 μs | 0.0090 μs | 1 | 0.6866 | 0.0191 | 8.77 KB |
| | | | | | | | |
| MediatorSG_ColdStart | 9.911 μs | 0.0582 μs | 0.0544 μs | 1 | 2.5330 | 0.1984 | 32.4 KB |

## Realistic Pipeline - All Libraries

| Method | Mean | Error | StdDev | Ratio | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_RealisticPipeline | 666.9 ns | 4.61 ns | 4.31 ns | 0.99 | 1 | 0.0191 | 255 B | 0.94 |
| DirectCall_WithPipeline | 674.1 ns | 3.60 ns | 3.37 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| | | | | | | | | |
| DirectCall_WithPipeline | 713.9 ns | 3.65 ns | 3.41 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| MediatR_RealisticPipeline | 857.1 ns | 2.38 ns | 2.23 ns | 1.20 | 2 | 0.0782 | 1032 B | 3.82 |
| | | | | | | | | |
| DirectCall_WithPipeline | 661.2 ns | 5.18 ns | 4.85 ns | 1.00 | 1 | 0.0200 | 271 B | 1.00 |
| DispatchR_RealisticPipeline | 666.7 ns | 2.72 ns | 2.54 ns | 1.01 | 1 | 0.0191 | 255 B | 0.94 |
| | | | | | | | | |
| DirectCall_WithPipeline | 679.2 ns | 4.57 ns | 4.27 ns | 1.00 | 1 | 0.0200 | 270 B | 1.00 |
| MediatorSG_RealisticPipeline | 718.0 ns | 8.25 ns | 7.72 ns | 1.06 | 2 | 0.0296 | 397 B | 1.47 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/results/`.
