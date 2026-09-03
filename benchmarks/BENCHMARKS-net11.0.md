# Benchmarks

Target framework: `net11.0`

> **These numbers need .NET 11 Preview 7 or later, with runtime-async on** (the switch lives in
> `benchmarks/Directory.Build.props`). Two Preview 7 runtime changes carry most of the difference
> against `net10.0`, and neither exists in Preview 6:
>
> - Async methods now go through tiered compilation. Before, they ran tier0 code forever, so every
>   `async` method on the dispatch path stayed unoptimized no matter how hot it got.
> - A hot `await` on an already-completed task folds into a status-flag check instead of a helper
>   call, which is the shape this library is built around.
>
> Preview 7 also fixed a flag check that skipped saving and restoring the async context across an
> `await` in a `ValueTask`-returning method. That is the entire dispatch surface here, so anything
> measured on an earlier preview with runtime-async on is unreliable for code that reads
> `AsyncLocal`, `Activity.Current` or `IHttpContextAccessor`.

```
BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 9.29 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 11.0.0 (11.0.0-preview.7.26381.103, 11.0.26.38203), X64 RyuJIT x86-64-v3
```

> **Note:** Each library's benchmarks run in **isolated processes** (only that library active).
> The `All Libraries` sections below concatenate those isolated results for easy comparison.

## DSoft - Send (No Behaviors)

| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DirectCall | 2.090 ns | 0.0081 ns | 0.0076 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send | 2.489 ns | 0.0074 ns | 0.0069 ns |  1.19 |    2 |         - |          NA |

## DSoft - Send (Behaviors)

| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DirectCall            | 2.085 ns | 0.0054 ns | 0.0048 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send_3Behaviors | 5.381 ns | 0.0092 ns | 0.0086 ns |  2.58 |    2 |         - |          NA |
| DSoft_Send_5Behaviors | 6.425 ns | 0.0114 ns | 0.0106 ns |  3.08 |    3 |         - |          NA |

## DSoft - Send (Object)

| Method             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic | 2.478 ns | 0.0122 ns | 0.0108 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DSoft_Send_Object  | 5.585 ns | 0.0775 ns | 0.0725 ns |  2.25 |    0.03 |    2 | 0.0018 |      24 B |          NA |

## DSoft - Publish

| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 1.346 ns | 0.0032 ns | 0.0029 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 2.349 ns | 0.0042 ns | 0.0040 ns |  1.75 |    2 |         - |          NA |

## DSoft - Publish (Object)

| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 2.379 ns | 0.0038 ns | 0.0034 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish_Object  | 3.992 ns | 0.0064 ns | 0.0057 ns |  1.68 |    2 |         - |          NA |

## DSoft - Stream

| Method        | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DSoft_Stream  | 31.13 ns | 0.277 ns | 0.259 ns |  0.98 |    1 | 0.0067 |      88 B |        1.00 |
| Direct_Stream | 31.80 ns | 0.196 ns | 0.184 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |

## DSoft - Concurrency

| Method            | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,287.13 ns | 2.821 ns | 2.501 ns |  0.99 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,295.62 ns | 8.462 ns | 7.915 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    35.79 ns | 0.682 ns | 0.638 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DSoft_Throughput  | Throughput |    45.49 ns | 0.078 ns | 0.073 ns |  1.27 |    0.02 |    2 |      - |      - |         - |          NA |

## DSoft - Cold Start

| Method          | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|---------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DSoft_ColdStart | 1.930 μs | 0.0114 μs | 0.0101 μs |    1 | 0.8659 | 0.0286 |  11.07 KB |

## DSoft - Realistic Pipeline

| Method                  | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline |  92.70 ns | 0.507 ns | 0.474 ns |  1.00 |    1 | 0.0141 |     184 B |        1.00 |
| DSoft_RealisticPipeline | 102.84 ns | 0.404 ns | 0.378 ns |  1.11 |    2 | 0.0122 |     160 B |        0.87 |

## DSoft - Behavior Scaling

| Method          | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      | 1.901 ns | 0.0066 ns | 0.0062 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 2.371 ns | 0.0101 ns | 0.0094 ns |  1.25 |    0.01 |    2 |         - |          NA |
| Send_1Behaviors | 4.366 ns | 0.0063 ns | 0.0056 ns |  2.30 |    0.01 |    3 |         - |          NA |
| Send_2Behaviors | 4.857 ns | 0.0073 ns | 0.0068 ns |  2.56 |    0.01 |    4 |         - |          NA |
| Send_3Behaviors | 5.390 ns | 0.0173 ns | 0.0153 ns |  2.84 |    0.01 |    5 |         - |          NA |
| Send_5Behaviors | 6.405 ns | 0.0091 ns | 0.0085 ns |  3.37 |    0.01 |    6 |         - |          NA |
| Send_8Behaviors | 9.056 ns | 0.0155 ns | 0.0137 ns |  4.76 |    0.02 |    7 |         - |          NA |

## MediatR - Send (No Behaviors)

| Method       | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   |  1.800 ns | 0.0180 ns | 0.0168 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Send | 42.867 ns | 0.1506 ns | 0.1409 ns | 23.82 |    0.23 |    2 | 0.0190 |     248 B |          NA |

## MediatR - Send (Behaviors)

| Method                  | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   1.812 ns | 0.0144 ns | 0.0135 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Send_3Behaviors | 109.754 ns | 0.3491 ns | 0.2915 ns | 60.57 |    0.46 |    2 | 0.0575 |     752 B |          NA |
| MediatR_Send_5Behaviors | 140.198 ns | 0.6573 ns | 0.5489 ns | 77.37 |    0.63 |    3 | 0.0782 |    1024 B |          NA |

## MediatR - Send (Object)

| Method               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Send_Generic | 41.78 ns | 0.202 ns | 0.189 ns |  1.00 |    1 | 0.0190 |     248 B |        1.00 |
| MediatR_Send_Object  | 45.38 ns | 0.219 ns | 0.194 ns |  1.09 |    2 | 0.0220 |     288 B |        1.16 |

## MediatR - Publish

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish  |   1.364 ns | 0.0062 ns | 0.0058 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Publish | 110.903 ns | 0.4036 ns | 0.3578 ns | 81.30 |    0.42 |    2 | 0.0575 |     752 B |          NA |

## MediatR - Publish (Object)

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 107.9 ns | 0.48 ns | 0.42 ns |  0.93 |    1 | 0.0575 |     752 B |        1.00 |
| MediatR_Publish_Generic | 115.8 ns | 0.52 ns | 0.49 ns |  1.00 |    2 | 0.0575 |     752 B |        1.00 |

## MediatR - Stream

| Method         | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  30.43 ns | 0.178 ns | 0.166 ns |  1.00 |    0.00 |    1 | 0.0067 |      88 B |        1.00 |
| MediatR_Stream | 106.23 ns | 0.232 ns | 0.217 ns |  3.49 |    0.02 |    2 | 0.0354 |     464 B |        5.27 |

## MediatR - Concurrency

| Method             | Categories | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |------------:|----------:|----------:|-------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,251.65 ns |  3.279 ns |  2.907 ns |   1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatR_FanOut     | FanOut     | 4,511.74 ns | 20.858 ns | 17.418 ns |   3.60 |    0.02 |    2 | 1.5640 | 0.0381 |   20536 B |        2.41 |
|                    |            |             |           |           |        |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |    34.60 ns |  0.042 ns |  0.037 ns |   1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatR_Throughput | Throughput | 4,011.28 ns | 18.817 ns | 16.681 ns | 115.92 |    0.48 |    2 | 1.8921 |      - |   24800 B |          NA |

## MediatR - Cold Start

| Method            | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|------------------ |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| MediatR_ColdStart | 3.212 μs | 0.0203 μs | 0.0190 μs |    1 | 0.9766 | 0.0305 |  12.49 KB |

## MediatR - Realistic Pipeline

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   |  94.79 ns | 0.437 ns | 0.409 ns |  1.00 |    0.00 |    1 | 0.0141 |     184 B |        1.00 |
| MediatR_RealisticPipeline | 354.12 ns | 1.538 ns | 1.439 ns |  3.74 |    0.02 |    2 | 0.0792 |    1039 B |        5.65 |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall     |  1.803 ns | 0.0109 ns | 0.0097 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Send | 27.221 ns | 0.0345 ns | 0.0288 ns | 15.10 |    0.08 |    2 |         - |          NA |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                |  1.883 ns | 0.0027 ns | 0.0024 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Send_5Behaviors | 49.538 ns | 0.1137 ns | 0.1008 ns | 26.31 |    0.06 |    2 |         - |          NA |
| DispatchR_Send_3Behaviors | 50.272 ns | 0.0909 ns | 0.0759 ns | 26.69 |    0.05 |    2 |         - |          NA |

## DispatchR - Publish

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  1.365 ns | 0.0058 ns | 0.0054 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Publish | 39.380 ns | 0.1080 ns | 0.1010 ns | 28.84 |    0.13 |    2 |         - |          NA |

## DispatchR - Publish (Object)

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  31.54 ns | 0.131 ns | 0.109 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 198.99 ns | 0.477 ns | 0.423 ns |  6.31 |    0.02 |    2 | 0.0196 |     256 B |          NA |

## DispatchR - Stream

| Method           | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 30.68 ns | 0.125 ns | 0.117 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| DispatchR_Stream | 58.43 ns | 0.179 ns | 0.168 ns |  1.90 |    2 | 0.0067 |      88 B |        1.00 |

## DispatchR - Concurrency

| Method               | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,296.16 ns | 5.089 ns | 4.761 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,711.58 ns | 6.922 ns | 5.780 ns |  2.86 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    35.40 ns | 0.046 ns | 0.040 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DispatchR_Throughput | Throughput | 2,557.63 ns | 5.798 ns | 5.423 ns | 72.25 |    0.17 |    2 |      - |      - |         - |          NA |

## DispatchR - Cold Start

| Method              | Mean     | Error     | StdDev    | Rank | Gen0   | Gen1   | Allocated |
|-------------------- |---------:|----------:|----------:|-----:|-------:|-------:|----------:|
| DispatchR_ColdStart | 1.721 μs | 0.0121 μs | 0.0101 μs |    1 | 0.6771 | 0.0191 |   8.66 KB |

## DispatchR - Realistic Pipeline

| Method                      | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline     |  89.81 ns | 0.456 ns | 0.404 ns |  1.00 |    0.00 |    1 | 0.0141 |     184 B |        1.00 |
| DispatchR_RealisticPipeline | 233.36 ns | 3.839 ns | 3.591 ns |  2.60 |    0.04 |    2 | 0.0212 |     279 B |        1.52 |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DirectCall      | 1.893 ns | 0.0036 ns | 0.0032 ns |  1.00 |    1 |         - |          NA |
| MediatorSG_Send | 5.804 ns | 0.0076 ns | 0.0067 ns |  3.07 |    2 |         - |          NA |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                 |  1.864 ns | 0.0049 ns | 0.0044 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Send_3Behaviors | 23.562 ns | 0.0743 ns | 0.0695 ns | 12.64 |    0.05 |    2 |         - |          NA |
| MediatorSG_Send_5Behaviors | 29.064 ns | 0.0651 ns | 0.0609 ns | 15.59 |    0.05 |    3 |         - |          NA |

## Mediator (Source Gen) - Send (Object)

| Method                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Send_Generic | 5.944 ns | 0.0288 ns | 0.0270 ns |  1.00 |    1 |      - |         - |          NA |
| MediatorSG_Send_Object  | 6.900 ns | 0.0272 ns | 0.0254 ns |  1.16 |    2 | 0.0018 |      24 B |          NA |

## Mediator (Source Gen) - Publish

| Method             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish     | 1.360 ns | 0.0056 ns | 0.0052 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Publish | 6.104 ns | 0.0124 ns | 0.0116 ns |  4.49 |    0.02 |    2 |         - |          NA |

## Mediator (Source Gen) - Publish (Object)

| Method                     | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  | 4.327 ns | 0.0128 ns | 0.0113 ns |  0.69 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 6.235 ns | 0.0257 ns | 0.0241 ns |  1.00 |    2 |         - |          NA |

## Mediator (Source Gen) - Stream

| Method            | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     | 30.47 ns | 0.127 ns | 0.119 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| MediatorSG_Stream | 31.71 ns | 0.118 ns | 0.110 ns |  1.04 |    2 | 0.0067 |      88 B |        1.00 |

## Mediator (Source Gen) - Concurrency

| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,306.52 ns | 6.177 ns | 5.778 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 1,618.57 ns | 4.189 ns | 3.919 ns |  1.24 |    0.01 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    36.03 ns | 0.719 ns | 1.259 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatorSG_Throughput | Throughput |   408.69 ns | 0.810 ns | 0.757 ns | 11.36 |    0.43 |    2 |      - |      - |         - |          NA |

## Mediator (Source Gen) - Cold Start

| Method               | Mean     | Error    | StdDev   | Rank | Gen0   | Gen1   | Allocated |
|--------------------- |---------:|---------:|---------:|-----:|-------:|-------:|----------:|
| MediatorSG_ColdStart | 10.04 μs | 0.090 μs | 0.084 μs |    1 | 2.8534 | 0.2594 |  36.44 KB |

## Mediator (Source Gen) - Realistic Pipeline

| Method                       | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      |  88.63 ns | 0.347 ns | 0.308 ns |  1.00 |    0.00 |    1 | 0.0141 |     184 B |        1.00 |
| MediatorSG_RealisticPipeline | 229.69 ns | 1.208 ns | 1.130 ns |  2.59 |    0.02 |    2 | 0.0274 |     359 B |        1.95 |

## Send - All Libraries (No Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 2.090 ns | 0.0081 ns | 0.0076 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Send | 2.489 ns | 0.0074 ns | 0.0069 ns | 1.19 | - | 2 | - | - | NA |
| | | | | | | | | | |
| DirectCall | 1.800 ns | 0.0180 ns | 0.0168 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatR_Send | 42.867 ns | 0.1506 ns | 0.1409 ns | 23.82 | 0.23 | 2 | 0.0190 | 248 B | NA |
| | | | | | | | | | |
| DirectCall | 1.803 ns | 0.0109 ns | 0.0097 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Send | 27.221 ns | 0.0345 ns | 0.0288 ns | 15.10 | 0.08 | 2 | - | - | NA |
| | | | | | | | | | |
| DirectCall | 1.893 ns | 0.0036 ns | 0.0032 ns | 1.00 | - | 1 | - | - | NA |
| MediatorSG_Send | 5.804 ns | 0.0076 ns | 0.0067 ns | 3.07 | - | 2 | - | - | NA |

## Send - All Libraries (Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 2.085 ns | 0.0054 ns | 0.0048 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Send_3Behaviors | 5.381 ns | 0.0092 ns | 0.0086 ns | 2.58 | - | 2 | - | - | NA |
| DSoft_Send_5Behaviors | 6.425 ns | 0.0114 ns | 0.0106 ns | 3.08 | - | 3 | - | - | NA |
| | | | | | | | | | |
| DirectCall | 1.812 ns | 0.0144 ns | 0.0135 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatR_Send_3Behaviors | 109.754 ns | 0.3491 ns | 0.2915 ns | 60.57 | 0.46 | 2 | 0.0575 | 752 B | NA |
| MediatR_Send_5Behaviors | 140.198 ns | 0.6573 ns | 0.5489 ns | 77.37 | 0.63 | 3 | 0.0782 | 1024 B | NA |
| | | | | | | | | | |
| DirectCall | 1.883 ns | 0.0027 ns | 0.0024 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Send_5Behaviors | 49.538 ns | 0.1137 ns | 0.1008 ns | 26.31 | 0.06 | 2 | - | - | NA |
| DispatchR_Send_3Behaviors | 50.272 ns | 0.0909 ns | 0.0759 ns | 26.69 | 0.05 | 2 | - | - | NA |
| | | | | | | | | | |
| DirectCall | 1.864 ns | 0.0049 ns | 0.0044 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatorSG_Send_3Behaviors | 23.562 ns | 0.0743 ns | 0.0695 ns | 12.64 | 0.05 | 2 | - | - | NA |
| MediatorSG_Send_5Behaviors | 29.064 ns | 0.0651 ns | 0.0609 ns | 15.59 | 0.05 | 3 | - | - | NA |

## Send (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send_Generic | 2.478 ns | 0.0122 ns | 0.0108 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DSoft_Send_Object | 5.585 ns | 0.0775 ns | 0.0725 ns | 2.25 | 0.03 | 2 | 0.0018 | 24 B | NA |
| | | | | | | | | | |
| MediatR_Send_Generic | 41.78 ns | 0.202 ns | 0.189 ns | 1.00 | - | 1 | 0.0190 | 248 B | 1.00 |
| MediatR_Send_Object | 45.38 ns | 0.219 ns | 0.194 ns | 1.09 | - | 2 | 0.0220 | 288 B | 1.16 |
| | | | | | | | | | |
| MediatorSG_Send_Generic | 5.944 ns | 0.0288 ns | 0.0270 ns | 1.00 | - | 1 | - | - | NA |
| MediatorSG_Send_Object | 6.900 ns | 0.0272 ns | 0.0254 ns | 1.16 | - | 2 | 0.0018 | 24 B | NA |

## Publish - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Publish | 1.346 ns | 0.0032 ns | 0.0029 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish | 2.349 ns | 0.0042 ns | 0.0040 ns | 1.75 | - | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 1.364 ns | 0.0062 ns | 0.0058 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatR_Publish | 110.903 ns | 0.4036 ns | 0.3578 ns | 81.30 | 0.42 | 2 | 0.0575 | 752 B | NA |
| | | | | | | | | | |
| Direct_Publish | 1.365 ns | 0.0058 ns | 0.0054 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish | 39.380 ns | 0.1080 ns | 0.1010 ns | 28.84 | 0.13 | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 1.360 ns | 0.0056 ns | 0.0052 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatorSG_Publish | 6.104 ns | 0.0124 ns | 0.0116 ns | 4.49 | 0.02 | 2 | - | - | NA |

## Publish (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Publish_Generic | 2.379 ns | 0.0038 ns | 0.0034 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish_Object | 3.992 ns | 0.0064 ns | 0.0057 ns | 1.68 | - | 2 | - | - | NA |
| | | | | | | | | | |
| MediatR_Publish_Object | 107.9 ns | 0.48 ns | 0.42 ns | 0.93 | - | 1 | 0.0575 | 752 B | 1.00 |
| MediatR_Publish_Generic | 115.8 ns | 0.52 ns | 0.49 ns | 1.00 | - | 2 | 0.0575 | 752 B | 1.00 |
| | | | | | | | | | |
| DispatchR_Publish_Generic | 31.54 ns | 0.131 ns | 0.109 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish_Object | 198.99 ns | 0.477 ns | 0.423 ns | 6.31 | 0.02 | 2 | 0.0196 | 256 B | NA |
| | | | | | | | | | |
| MediatorSG_Publish_Object | 4.327 ns | 0.0128 ns | 0.0113 ns | 0.69 | - | 1 | - | - | NA |
| MediatorSG_Publish_Generic | 6.235 ns | 0.0257 ns | 0.0241 ns | 1.00 | - | 2 | - | - | NA |

## Stream - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Stream | 31.13 ns | 0.277 ns | 0.259 ns | 0.98 | - | 1 | 0.0067 | 88 B | 1.00 |
| Direct_Stream | 31.80 ns | 0.196 ns | 0.184 ns | 1.00 | - | 1 | 0.0067 | 88 B | 1.00 |
| | | | | | | | | | |
| Direct_Stream | 30.43 ns | 0.178 ns | 0.166 ns | 1.00 | 0.00 | 1 | 0.0067 | 88 B | 1.00 |
| MediatR_Stream | 106.23 ns | 0.232 ns | 0.217 ns | 3.49 | 0.02 | 2 | 0.0354 | 464 B | 5.27 |
| | | | | | | | | | |
| Direct_Stream | 30.68 ns | 0.125 ns | 0.117 ns | 1.00 | - | 1 | 0.0067 | 88 B | 1.00 |
| DispatchR_Stream | 58.43 ns | 0.179 ns | 0.168 ns | 1.90 | - | 2 | 0.0067 | 88 B | 1.00 |
| | | | | | | | | | |
| Direct_Stream | 30.47 ns | 0.127 ns | 0.119 ns | 1.00 | - | 1 | 0.0067 | 88 B | 1.00 |
| MediatorSG_Stream | 31.71 ns | 0.118 ns | 0.110 ns | 1.04 | - | 2 | 0.0067 | 88 B | 1.00 |

## Concurrency - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_FanOut | 1,287.13 ns | 2.821 ns | 2.501 ns | 0.99 | 0.01 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_FanOut | 1,295.62 ns | 8.462 ns | 7.915 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 35.79 ns | 0.682 ns | 0.638 ns | 1.00 | 0.00 | 1 | - | - | - | NA |
| DSoft_Throughput | 45.49 ns | 0.078 ns | 0.073 ns | 1.27 | 0.02 | 2 | - | - | - | NA |
| | | | | | | | | | | |
| Direct_FanOut | 1,251.65 ns | 3.279 ns | 2.907 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatR_FanOut | 4,511.74 ns | 20.858 ns | 17.418 ns | 3.60 | 0.02 | 2 | 1.5640 | 0.0381 | 20536 B | 2.41 |
| Direct_Throughput | 34.60 ns | 0.042 ns | 0.037 ns | 1.00 | 0.00 | 1 | - | - | - | NA |
| MediatR_Throughput | 4,011.28 ns | 18.817 ns | 16.681 ns | 115.92 | 0.48 | 2 | 1.8921 | - | 24800 B | NA |
| | | | | | | | | | | |
| Direct_FanOut | 1,296.16 ns | 5.089 ns | 4.761 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DispatchR_FanOut | 3,711.58 ns | 6.922 ns | 5.780 ns | 2.86 | 0.01 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 35.40 ns | 0.046 ns | 0.040 ns | 1.00 | 0.00 | 1 | - | - | - | NA |
| DispatchR_Throughput | 2,557.63 ns | 5.798 ns | 5.423 ns | 72.25 | 0.17 | 2 | - | - | - | NA |
| | | | | | | | | | | |
| Direct_FanOut | 1,306.52 ns | 6.177 ns | 5.778 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatorSG_FanOut | 1,618.57 ns | 4.189 ns | 3.919 ns | 1.24 | 0.01 | 2 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 36.03 ns | 0.719 ns | 1.259 ns | 1.00 | 0.00 | 1 | - | - | - | NA |
| MediatorSG_Throughput | 408.69 ns | 0.810 ns | 0.757 ns | 11.36 | 0.43 | 2 | - | - | - | NA |

## Cold Start - All Libraries

| Method | Mean | Error | StdDev | Rank | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_ColdStart | 1.930 μs | 0.0114 μs | 0.0101 μs | 1 | 0.8659 | 0.0286 | 11.07 KB |
| | | | | | | | |
| MediatR_ColdStart | 3.212 μs | 0.0203 μs | 0.0190 μs | 1 | 0.9766 | 0.0305 | 12.49 KB |
| | | | | | | | |
| DispatchR_ColdStart | 1.721 μs | 0.0121 μs | 0.0101 μs | 1 | 0.6771 | 0.0191 | 8.66 KB |
| | | | | | | | |
| MediatorSG_ColdStart | 10.04 μs | 0.090 μs | 0.084 μs | 1 | 2.8534 | 0.2594 | 36.44 KB |

## Realistic Pipeline - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall_WithPipeline | 92.70 ns | 0.507 ns | 0.474 ns | 1.00 | - | 1 | 0.0141 | 184 B | 1.00 |
| DSoft_RealisticPipeline | 102.84 ns | 0.404 ns | 0.378 ns | 1.11 | - | 2 | 0.0122 | 160 B | 0.87 |
| | | | | | | | | | |
| DirectCall_WithPipeline | 94.79 ns | 0.437 ns | 0.409 ns | 1.00 | 0.00 | 1 | 0.0141 | 184 B | 1.00 |
| MediatR_RealisticPipeline | 354.12 ns | 1.538 ns | 1.439 ns | 3.74 | 0.02 | 2 | 0.0792 | 1039 B | 5.65 |
| | | | | | | | | | |
| DirectCall_WithPipeline | 89.81 ns | 0.456 ns | 0.404 ns | 1.00 | 0.00 | 1 | 0.0141 | 184 B | 1.00 |
| DispatchR_RealisticPipeline | 233.36 ns | 3.839 ns | 3.591 ns | 2.60 | 0.04 | 2 | 0.0212 | 279 B | 1.52 |
| | | | | | | | | | |
| DirectCall_WithPipeline | 88.63 ns | 0.347 ns | 0.308 ns | 1.00 | 0.00 | 1 | 0.0141 | 184 B | 1.00 |
| MediatorSG_RealisticPipeline | 229.69 ns | 1.208 ns | 1.130 ns | 2.59 | 0.02 | 2 | 0.0274 | 359 B | 1.95 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/<tfm>/results/`.
