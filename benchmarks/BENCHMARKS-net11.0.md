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
BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700F 2.10GHz, 1 CPU, 20 logical and 12 physical cores
Memory: 15.84 GB Total, 8.32 GB Available
.NET SDK 11.0.100-rc.1.26425.128
  [Host]     : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3
  Job-NZIUHH : .NET 11.0.0 (11.0.0-rc.1.26425.128, 11.0.26.42628), X64 RyuJIT x86-64-v3

IterationCount=30  WarmupCount=12
```

> **Note:** Each library's benchmarks run in **isolated processes** (only that library active).
> The `All Libraries` sections below concatenate those isolated results for easy comparison.

## DSoft - Send (No Behaviors)

| Method     | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|----------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DirectCall | 1.905 ns | 0.0056 ns | 0.0084 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send | 2.678 ns | 0.0027 ns | 0.0039 ns |  1.41 |    2 |         - |          NA |

## DSoft - Send (Behaviors)

| Method                | Mean     | Error     | StdDev    | Median   | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|---------:|------:|-----:|----------:|------------:|
| DirectCall            | 2.113 ns | 0.0058 ns | 0.0087 ns | 2.111 ns |  1.00 |    1 |         - |          NA |
| DSoft_Send_3Behaviors | 5.564 ns | 0.0159 ns | 0.0228 ns | 5.565 ns |  2.63 |    2 |         - |          NA |
| DSoft_Send_5Behaviors | 6.551 ns | 0.0107 ns | 0.0156 ns | 6.554 ns |  3.10 |    3 |         - |          NA |

## DSoft - Send (Object)

| Method             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DSoft_Send_Generic | 2.552 ns | 0.0061 ns | 0.0091 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DSoft_Send_Object  | 6.646 ns | 0.0258 ns | 0.0386 ns |  2.60 |    0.02 |    2 | 0.0018 |      24 B |          NA |

## DSoft - Publish

| Method         | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| Direct_Publish | 1.460 ns | 0.0037 ns | 0.0056 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish  | 2.359 ns | 0.0043 ns | 0.0065 ns |  1.62 |    2 |         - |          NA |

## DSoft - Publish (Object)

| Method                | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| DSoft_Publish_Generic | 2.407 ns | 0.0037 ns | 0.0053 ns |  1.00 |    1 |         - |          NA |
| DSoft_Publish_Object  | 3.753 ns | 0.0035 ns | 0.0050 ns |  1.56 |    2 |         - |          NA |

## DSoft - Stream

| Method        | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream | 30.12 ns | 0.069 ns | 0.103 ns | 30.11 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| DSoft_Stream  | 30.72 ns | 0.138 ns | 0.207 ns | 30.61 ns |  1.02 |    1 | 0.0067 |      88 B |        1.00 |

## DSoft - Concurrency

| Method            | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DSoft_FanOut      | FanOut     | 1,329.28 ns | 3.984 ns | 5.963 ns |  0.97 |    0.01 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| Direct_FanOut     | FanOut     | 1,374.96 ns | 4.174 ns | 6.119 ns |  1.00 |    0.00 |    2 | 0.6523 | 0.0172 |    8536 B |        1.00 |
|                   |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput | Throughput |    36.49 ns | 0.778 ns | 1.165 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DSoft_Throughput  | Throughput |    45.72 ns | 0.095 ns | 0.140 ns |  1.25 |    0.04 |    2 |      - |      - |         - |          NA |

## DSoft - Cold Start

> **Three rows, each a superset of the one above.** `Startup_DiFloor` registers one trivial service
> in a container, builds it and resolves it — no mediator involved, the floor every library pays and
> none of them causes. `Startup_Registered` instead registers the library and resolves the mediator.
> `Startup_FirstRequest` adds the first dispatch.
>
> So `Registered - DiFloor` is what standing the library up costs, `FirstRequest - Registered` is the
> first dispatch, and **`FirstRequest - DiFloor` is everything the library adds to
> time-to-first-request** — the number an application actually feels.
>
> The floor resolves rather than merely building, and that matters more than it looks: on a container
> holding one trivial service, building took 6.80 ms and the first resolve another 5.44 ms. A
> baseline that only built would leave those 5.44 ms to be charged to whichever library the row
> belongs to. Registration is inside the measurement for the same reason — it used to sit in setup,
> which left it out of every row and pre-compiled the machinery the measured row then reused.
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean.

| Method                     | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|--------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| DSoft_Startup_DiFloor      | 11.67 ms | 0.121 ms | 0.215 ms | 11.64 ms |  1.00 |    0.00 | baseline |    1 |   7.42 KB |        1.00 |
| DSoft_Startup_Registered   | 30.51 ms | 0.230 ms | 0.408 ms | 30.42 ms |  2.61 |    0.06 | 18.78 ms |    2 |   28.4 KB |        3.83 |
| DSoft_Startup_FirstRequest | 34.77 ms | 0.650 ms | 1.156 ms | 34.37 ms |  2.98 |    0.11 | 22.73 ms |    3 |  32.48 KB |        4.38 |

## DSoft - Realistic Pipeline

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline | 100.1 ns | 0.16 ns | 0.23 ns |  1.00 |    1 | 0.0129 |     168 B |        1.00 |
| DSoft_RealisticPipeline | 111.9 ns | 0.52 ns | 0.70 ns |  1.12 |    2 | 0.0110 |     144 B |        0.86 |

## DSoft - Behavior Scaling

| Method          | Mean     | Error     | StdDev    | Median   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|----------:|---------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      | 1.885 ns | 0.0051 ns | 0.0075 ns | 1.884 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 2.471 ns | 0.0034 ns | 0.0048 ns | 2.470 ns |  1.31 |    0.01 |    2 |         - |          NA |
| Send_1Behaviors | 4.433 ns | 0.0097 ns | 0.0145 ns | 4.431 ns |  2.35 |    0.01 |    3 |         - |          NA |
| Send_2Behaviors | 5.048 ns | 0.0146 ns | 0.0219 ns | 5.047 ns |  2.68 |    0.02 |    4 |         - |          NA |
| Send_3Behaviors | 5.576 ns | 0.0103 ns | 0.0145 ns | 5.576 ns |  2.96 |    0.01 |    5 |         - |          NA |
| Send_5Behaviors | 6.611 ns | 0.0542 ns | 0.0742 ns | 6.583 ns |  3.51 |    0.04 |    6 |         - |          NA |
| Send_8Behaviors | 9.270 ns | 0.0110 ns | 0.0157 ns | 9.267 ns |  4.92 |    0.02 |    7 |         - |          NA |

## MediatR - Send (No Behaviors)

| Method       | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------- |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall   |  1.895 ns | 0.0031 ns | 0.0046 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Send | 40.928 ns | 0.0896 ns | 0.1341 ns | 21.60 |    0.09 |    2 | 0.0190 |     248 B |          NA |

## MediatR - Send (Behaviors)

| Method                  | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall              |   1.898 ns | 0.0056 ns | 0.0084 ns |   1.899 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Send_3Behaviors | 107.288 ns | 0.6783 ns | 1.0152 ns | 107.221 ns | 56.54 |    0.58 |    2 | 0.0575 |     752 B |          NA |
| MediatR_Send_5Behaviors | 143.534 ns | 1.1071 ns | 1.6228 ns | 143.570 ns | 75.64 |    0.90 |    3 | 0.0782 |    1024 B |          NA |

## MediatR - Send (Object)

| Method               | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------------- |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Send_Generic | 42.79 ns | 0.082 ns | 0.123 ns |  1.00 |    1 | 0.0190 |     248 B |        1.00 |
| MediatR_Send_Object  | 46.14 ns | 0.332 ns | 0.496 ns |  1.08 |    2 | 0.0220 |     288 B |        1.16 |

## MediatR - Publish

| Method          | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Publish  |   1.469 ns | 0.0031 ns | 0.0046 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| MediatR_Publish | 112.916 ns | 0.1734 ns | 0.2595 ns | 76.86 |    0.29 |    2 | 0.0575 |     752 B |          NA |

## MediatR - Publish (Object)

| Method                  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|--------:|--------:|------:|-----:|-------:|----------:|------------:|
| MediatR_Publish_Object  | 111.6 ns | 0.31 ns | 0.46 ns |  0.94 |    1 | 0.0575 |     752 B |        1.00 |
| MediatR_Publish_Generic | 118.4 ns | 0.36 ns | 0.53 ns |  1.00 |    2 | 0.0575 |     752 B |        1.00 |

## MediatR - Stream

| Method         | Mean      | Error    | StdDev   | Median    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------:|---------:|---------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| Direct_Stream  |  30.94 ns | 0.085 ns | 0.120 ns |  30.94 ns |  1.00 |    0.00 |    1 | 0.0067 |      88 B |        1.00 |
| MediatR_Stream | 112.76 ns | 1.507 ns | 2.209 ns | 112.53 ns |  3.64 |    0.07 |    2 | 0.0354 |     464 B |        5.27 |

## MediatR - Concurrency

| Method             | Categories | Mean        | Error     | StdDev    | Median      | Ratio  | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |----------- |------------:|----------:|----------:|------------:|-------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut      | FanOut     | 1,338.54 ns |  4.723 ns |  7.070 ns | 1,340.10 ns |   1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatR_FanOut     | FanOut     | 4,600.43 ns | 11.059 ns | 16.552 ns | 4,598.66 ns |   3.44 |    0.02 |    2 | 1.5640 | 0.0381 |   20536 B |        2.41 |
|                    |            |             |           |           |             |        |         |      |        |        |           |             |
| Direct_Throughput  | Throughput |    35.80 ns |  0.711 ns |  1.065 ns |    35.20 ns |   1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatR_Throughput | Throughput | 3,910.66 ns |  7.277 ns | 10.666 ns | 3,911.42 ns | 109.34 |    3.13 |    2 | 1.8921 |      - |   24800 B |          NA |

## MediatR - Cold Start

> **Three rows, each a superset of the one above.** `Startup_DiFloor` registers one trivial service
> in a container, builds it and resolves it — no mediator involved, the floor every library pays and
> none of them causes. `Startup_Registered` instead registers the library and resolves the mediator.
> `Startup_FirstRequest` adds the first dispatch.
>
> So `Registered - DiFloor` is what standing the library up costs, `FirstRequest - Registered` is the
> first dispatch, and **`FirstRequest - DiFloor` is everything the library adds to
> time-to-first-request** — the number an application actually feels.
>
> The floor resolves rather than merely building, and that matters more than it looks: on a container
> holding one trivial service, building took 6.80 ms and the first resolve another 5.44 ms. A
> baseline that only built would leave those 5.44 ms to be charged to whichever library the row
> belongs to. Registration is inside the measurement for the same reason — it used to sit in setup,
> which left it out of every row and pre-compiled the machinery the measured row then reused.
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean.

| Method                       | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated  | Alloc Ratio |
|----------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|-----------:|------------:|
| MediatR_Startup_DiFloor      | 11.65 ms | 0.114 ms | 0.203 ms | 11.63 ms |  1.00 |    0.00 | baseline |    1 |    7.42 KB |        1.00 |
| MediatR_Startup_Registered   | 42.77 ms | 3.811 ms | 6.773 ms | 41.69 ms |  3.67 |    0.58 | 30.05 ms |    2 | 1624.42 KB |      218.87 |
| MediatR_Startup_FirstRequest | 43.20 ms | 0.208 ms | 0.371 ms | 43.14 ms |  3.71 |    0.07 | 31.51 ms |    3 | 1680.12 KB |      226.37 |

## MediatR - Realistic Pipeline

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline   |  98.57 ns | 0.345 ns | 0.495 ns |  1.00 |    0.00 |    1 | 0.0129 |     168 B |        1.00 |
| MediatR_RealisticPipeline | 350.62 ns | 0.585 ns | 0.781 ns |  3.56 |    0.02 |    2 | 0.0777 |    1016 B |        6.05 |

## MediatR - Behavior Scaling

| Method          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| DirectCall      |   1.897 ns | 0.0054 ns | 0.0080 ns |   1.896 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| Send_0Behaviors |  41.210 ns | 0.2373 ns | 0.3551 ns |  41.271 ns | 21.73 |    0.21 |    2 | 0.0190 |      - |     248 B |          NA |
| Send_1Behaviors |  71.802 ns | 0.1944 ns | 0.2910 ns |  71.804 ns | 37.86 |    0.22 |    3 | 0.0367 |      - |     480 B |          NA |
| Send_2Behaviors |  88.604 ns | 0.2083 ns | 0.2780 ns |  88.643 ns | 46.72 |    0.24 |    4 | 0.0471 |      - |     616 B |          NA |
| Send_3Behaviors | 104.571 ns | 0.3013 ns | 0.4416 ns | 104.609 ns | 55.14 |    0.32 |    5 | 0.0575 |      - |     752 B |          NA |
| Send_5Behaviors | 136.119 ns | 0.5388 ns | 0.8064 ns | 136.152 ns | 71.77 |    0.51 |    6 | 0.0782 |      - |    1024 B |          NA |
| Send_8Behaviors | 188.066 ns | 0.5503 ns | 0.8066 ns | 187.977 ns | 99.16 |    0.59 |    7 | 0.1094 | 0.0002 |    1432 B |          NA |

## DispatchR - Send (No Behaviors)

| Method         | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------- |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall     |  1.891 ns | 0.0063 ns | 0.0094 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Send | 27.122 ns | 0.0905 ns | 0.1355 ns | 14.35 |    0.10 |    2 |         - |          NA |

## DispatchR - Send (Behaviors)

| Method                    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                |  1.886 ns | 0.0151 ns | 0.0226 ns |  1.887 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Send_3Behaviors | 31.563 ns | 0.0833 ns | 0.1222 ns | 31.526 ns | 16.74 |    0.21 |    2 |         - |          NA |
| DispatchR_Send_5Behaviors | 31.226 ns | 0.0608 ns | 0.0853 ns | 31.197 ns | 16.56 |    0.20 |    2 |         - |          NA |

## DispatchR - Publish

| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish    |  1.467 ns | 0.0015 ns | 0.0022 ns |  1.00 |    0.00 |    1 |         - |          NA |
| DispatchR_Publish | 32.301 ns | 0.0683 ns | 0.1022 ns | 22.02 |    0.08 |    2 |         - |          NA |

## DispatchR - Publish (Object)

| Method                    | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DispatchR_Publish_Generic |  33.43 ns | 0.066 ns | 0.095 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| DispatchR_Publish_Object  | 201.09 ns | 0.305 ns | 0.447 ns |  6.02 |    0.02 |    2 | 0.0196 |     256 B |          NA |

## DispatchR - Stream

| Method           | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream    | 31.15 ns | 0.084 ns | 0.121 ns | 31.16 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| DispatchR_Stream | 54.02 ns | 0.069 ns | 0.103 ns | 54.01 ns |  1.73 |    2 | 0.0067 |      88 B |        1.00 |

## DispatchR - Concurrency

| Method               | Categories | Mean        | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------- |----------- |------------:|----------:|----------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut        | FanOut     | 1,304.42 ns | 11.290 ns | 16.548 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| DispatchR_FanOut     | FanOut     | 3,693.85 ns |  6.257 ns |  8.974 ns |  2.83 |    0.04 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                      |            |             |           |           |       |         |      |        |        |           |             |
| Direct_Throughput    | Throughput |    34.66 ns |  0.089 ns |  0.127 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| DispatchR_Throughput | Throughput | 2,559.92 ns |  9.243 ns | 13.835 ns | 73.86 |    0.47 |    2 |      - |      - |         - |          NA |

## DispatchR - Cold Start

> **Three rows, each a superset of the one above.** `Startup_DiFloor` registers one trivial service
> in a container, builds it and resolves it — no mediator involved, the floor every library pays and
> none of them causes. `Startup_Registered` instead registers the library and resolves the mediator.
> `Startup_FirstRequest` adds the first dispatch.
>
> So `Registered - DiFloor` is what standing the library up costs, `FirstRequest - Registered` is the
> first dispatch, and **`FirstRequest - DiFloor` is everything the library adds to
> time-to-first-request** — the number an application actually feels.
>
> The floor resolves rather than merely building, and that matters more than it looks: on a container
> holding one trivial service, building took 6.80 ms and the first resolve another 5.44 ms. A
> baseline that only built would leave those 5.44 ms to be charged to whichever library the row
> belongs to. Registration is inside the measurement for the same reason — it used to sit in setup,
> which left it out of every row and pre-compiled the machinery the measured row then reused.
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean.

| Method                         | Mean     | Error    | StdDev    | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|----------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| DispatchR_Startup_DiFloor      | 13.51 ms | 5.522 ms |  9.816 ms | 11.95 ms |  1.00 |    0.00 | baseline |    1 |  19.47 KB |        1.00 |
| DispatchR_Startup_FirstRequest | 24.14 ms | 0.209 ms |  0.372 ms | 24.08 ms |  1.98 |    0.27 | 12.12 ms |    2 | 650.93 KB |       33.43 |
| DispatchR_Startup_Registered   | 24.48 ms | 9.630 ms | 17.117 ms | 21.75 ms |  2.01 |    1.42 | 9.79 ms  |    3 | 648.85 KB |       33.33 |

## DispatchR - Realistic Pipeline

| Method                      | Mean      | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline     |  99.99 ns | 0.250 ns | 0.366 ns |  1.00 |    0.00 |    1 | 0.0129 |     168 B |        1.00 |
| DispatchR_RealisticPipeline | 246.92 ns | 1.630 ns | 2.176 ns |  2.47 |    0.02 |    2 | 0.0200 |     263 B |        1.57 |

## DispatchR - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      |  2.088 ns | 0.0051 ns | 0.0074 ns |  2.088 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 26.670 ns | 0.0344 ns | 0.0505 ns | 26.665 ns | 12.77 |    0.05 |    2 |         - |          NA |
| Send_1Behaviors | 31.496 ns | 0.0920 ns | 0.1377 ns | 31.498 ns | 15.08 |    0.08 |    3 |         - |          NA |
| Send_2Behaviors | 31.338 ns | 0.0848 ns | 0.1270 ns | 31.301 ns | 15.01 |    0.08 |    3 |         - |          NA |
| Send_3Behaviors | 31.951 ns | 0.0825 ns | 0.1183 ns | 31.945 ns | 15.30 |    0.08 |    3 |         - |          NA |
| Send_5Behaviors | 31.824 ns | 0.0824 ns | 0.1207 ns | 31.817 ns | 15.24 |    0.08 |    3 |         - |          NA |
| Send_8Behaviors | 34.227 ns | 0.0978 ns | 0.1464 ns | 34.185 ns | 16.39 |    0.09 |    4 |         - |          NA |

## Mediator (Source Gen) - Send (No Behaviors)

| Method          | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      | 1.894 ns | 0.0068 ns | 0.0102 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Send | 9.798 ns | 0.0127 ns | 0.0187 ns |  5.17 |    0.03 |    2 |         - |          NA |

## Mediator (Source Gen) - Send (Behaviors)

| Method                     | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall                 |  1.731 ns | 0.0065 ns | 0.0097 ns |  1.730 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Send_3Behaviors | 19.421 ns | 0.0244 ns | 0.0357 ns | 19.419 ns | 11.22 |    0.06 |    2 |         - |          NA |
| MediatorSG_Send_5Behaviors | 27.229 ns | 0.1944 ns | 0.2909 ns | 27.089 ns | 15.73 |    0.19 |    3 |         - |          NA |

## Mediator (Source Gen) - Send (Object)

| Method                  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------ |---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| MediatorSG_Send_Generic | 11.13 ns | 0.024 ns | 0.035 ns |  1.00 |    1 |      - |         - |          NA |
| MediatorSG_Send_Object  | 13.51 ns | 0.096 ns | 0.140 ns |  1.21 |    2 | 0.0018 |      24 B |          NA |

## Mediator (Source Gen) - Publish

| Method             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Direct_Publish     | 1.454 ns | 0.0016 ns | 0.0023 ns |  1.00 |    0.00 |    1 |         - |          NA |
| MediatorSG_Publish | 6.319 ns | 0.0153 ns | 0.0215 ns |  4.35 |    0.02 |    2 |         - |          NA |

## Mediator (Source Gen) - Publish (Object)

| Method                     | Mean     | Error     | StdDev    | Ratio | Rank | Allocated | Alloc Ratio |
|--------------------------- |---------:|----------:|----------:|------:|-----:|----------:|------------:|
| MediatorSG_Publish_Object  | 4.076 ns | 0.0048 ns | 0.0072 ns |  0.65 |    1 |         - |          NA |
| MediatorSG_Publish_Generic | 6.261 ns | 0.0124 ns | 0.0185 ns |  1.00 |    2 |         - |          NA |

## Mediator (Source Gen) - Stream

| Method            | Mean     | Error    | StdDev   | Median   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------ |---------:|---------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| Direct_Stream     | 31.42 ns | 0.079 ns | 0.116 ns | 31.43 ns |  1.00 |    1 | 0.0067 |      88 B |        1.00 |
| MediatorSG_Stream | 31.78 ns | 0.157 ns | 0.230 ns | 31.81 ns |  1.01 |    1 | 0.0067 |      88 B |        1.00 |

## Mediator (Source Gen) - Concurrency

| Method                | Categories | Mean        | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------- |----------- |------------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Direct_FanOut         | FanOut     | 1,318.31 ns | 3.763 ns | 5.516 ns |  1.00 |    0.00 |    1 | 0.6523 | 0.0172 |    8536 B |        1.00 |
| MediatorSG_FanOut     | FanOut     | 2,028.31 ns | 3.160 ns | 4.730 ns |  1.54 |    0.01 |    2 | 0.6523 | 0.0153 |    8536 B |        1.00 |
|                       |            |             |          |          |       |         |      |        |        |           |             |
| Direct_Throughput     | Throughput |    36.22 ns | 0.774 ns | 1.135 ns |  1.00 |    0.00 |    1 |      - |      - |         - |          NA |
| MediatorSG_Throughput | Throughput |   782.16 ns | 6.181 ns | 9.251 ns | 21.62 |    0.76 |    2 |      - |      - |         - |          NA |

## Mediator (Source Gen) - Cold Start

> **Three rows, each a superset of the one above.** `Startup_DiFloor` registers one trivial service
> in a container, builds it and resolves it — no mediator involved, the floor every library pays and
> none of them causes. `Startup_Registered` instead registers the library and resolves the mediator.
> `Startup_FirstRequest` adds the first dispatch.
>
> So `Registered - DiFloor` is what standing the library up costs, `FirstRequest - Registered` is the
> first dispatch, and **`FirstRequest - DiFloor` is everything the library adds to
> time-to-first-request** — the number an application actually feels.
>
> The floor resolves rather than merely building, and that matters more than it looks: on a container
> holding one trivial service, building took 6.80 ms and the first resolve another 5.44 ms. A
> baseline that only built would leave those 5.44 ms to be charged to whichever library the row
> belongs to. Registration is inside the measurement for the same reason — it used to sit in setup,
> which left it out of every row and pre-compiled the machinery the measured row then reused.
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean.

| Method                          | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Added    | Rank | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|--------- |-----:|----------:|------------:|
| MediatorSG_Startup_DiFloor      | 11.63 ms | 0.107 ms | 0.191 ms | 11.65 ms |  1.00 |    0.00 | baseline |    1 |   7.42 KB |        1.00 |
| MediatorSG_Startup_Registered   | 14.74 ms | 0.123 ms | 0.219 ms | 14.74 ms |  1.27 |    0.03 | 3.09 ms  |    2 |   22.2 KB |        2.99 |
| MediatorSG_Startup_FirstRequest | 21.90 ms | 0.221 ms | 0.394 ms | 21.81 ms |  1.88 |    0.05 | 10.15 ms |    3 | 153.76 KB |       20.72 |

## Mediator (Source Gen) - Realistic Pipeline

| Method                       | Mean      | Error    | StdDev   | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |----------:|---------:|---------:|------:|-----:|-------:|----------:|------------:|
| DirectCall_WithPipeline      |  96.71 ns | 0.190 ns | 0.260 ns |  1.00 |    1 | 0.0129 |     168 B |        1.00 |
| MediatorSG_RealisticPipeline | 237.69 ns | 0.760 ns | 1.138 ns |  2.46 |    2 | 0.0255 |     335 B |        1.99 |

## Mediator (Source Gen) - Behavior Scaling

| Method          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|---------------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| DirectCall      |  1.888 ns | 0.0033 ns | 0.0048 ns |  1.887 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Send_0Behaviors | 10.529 ns | 0.0161 ns | 0.0236 ns | 10.536 ns |  5.58 |    0.02 |    2 |         - |          NA |
| Send_1Behaviors | 10.825 ns | 0.0254 ns | 0.0380 ns | 10.820 ns |  5.73 |    0.02 |    3 |         - |          NA |
| Send_2Behaviors | 13.070 ns | 0.1056 ns | 0.1514 ns | 13.018 ns |  6.92 |    0.08 |    4 |         - |          NA |
| Send_3Behaviors | 19.870 ns | 0.0297 ns | 0.0416 ns | 19.858 ns | 10.53 |    0.03 |    5 |         - |          NA |
| Send_5Behaviors | 26.466 ns | 0.0477 ns | 0.0714 ns | 26.459 ns | 14.02 |    0.05 |    6 |         - |          NA |
| Send_8Behaviors | 34.402 ns | 0.0672 ns | 0.1005 ns | 34.374 ns | 18.22 |    0.07 |    7 |         - |          NA |

## Send - All Libraries (No Behaviors)

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 1.905 ns | 0.0056 ns | 0.0084 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Send | 2.678 ns | 0.0027 ns | 0.0039 ns | 1.41 | - | 2 | - | - | NA |
| | | | | | | | | | |
| DirectCall | 1.895 ns | 0.0031 ns | 0.0046 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatR_Send | 40.928 ns | 0.0896 ns | 0.1341 ns | 21.60 | 0.09 | 2 | 0.0190 | 248 B | NA |
| | | | | | | | | | |
| DirectCall | 1.891 ns | 0.0063 ns | 0.0094 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Send | 27.122 ns | 0.0905 ns | 0.1355 ns | 14.35 | 0.10 | 2 | - | - | NA |
| | | | | | | | | | |
| DirectCall | 1.894 ns | 0.0068 ns | 0.0102 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatorSG_Send | 9.798 ns | 0.0127 ns | 0.0187 ns | 5.17 | 0.03 | 2 | - | - | NA |

## Send - All Libraries (Behaviors)

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall | 2.113 ns | 0.0058 ns | 0.0087 ns | 2.111 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Send_3Behaviors | 5.564 ns | 0.0159 ns | 0.0228 ns | 5.565 ns | 2.63 | - | 2 | - | - | NA |
| DSoft_Send_5Behaviors | 6.551 ns | 0.0107 ns | 0.0156 ns | 6.554 ns | 3.10 | - | 3 | - | - | NA |
| | | | | | | | | | | |
| DirectCall | 1.898 ns | 0.0056 ns | 0.0084 ns | 1.899 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatR_Send_3Behaviors | 107.288 ns | 0.6783 ns | 1.0152 ns | 107.221 ns | 56.54 | 0.58 | 2 | 0.0575 | 752 B | NA |
| MediatR_Send_5Behaviors | 143.534 ns | 1.1071 ns | 1.6228 ns | 143.570 ns | 75.64 | 0.90 | 3 | 0.0782 | 1024 B | NA |
| | | | | | | | | | | |
| DirectCall | 1.886 ns | 0.0151 ns | 0.0226 ns | 1.887 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Send_3Behaviors | 31.563 ns | 0.0833 ns | 0.1222 ns | 31.526 ns | 16.74 | 0.21 | 2 | - | - | NA |
| DispatchR_Send_5Behaviors | 31.226 ns | 0.0608 ns | 0.0853 ns | 31.197 ns | 16.56 | 0.20 | 2 | - | - | NA |
| | | | | | | | | | | |
| DirectCall | 1.731 ns | 0.0065 ns | 0.0097 ns | 1.730 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatorSG_Send_3Behaviors | 19.421 ns | 0.0244 ns | 0.0357 ns | 19.419 ns | 11.22 | 0.06 | 2 | - | - | NA |
| MediatorSG_Send_5Behaviors | 27.229 ns | 0.1944 ns | 0.2909 ns | 27.089 ns | 15.73 | 0.19 | 3 | - | - | NA |

## Send (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Send_Generic | 2.552 ns | 0.0061 ns | 0.0091 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DSoft_Send_Object | 6.646 ns | 0.0258 ns | 0.0386 ns | 2.60 | 0.02 | 2 | 0.0018 | 24 B | NA |
| | | | | | | | | | |
| MediatR_Send_Generic | 42.79 ns | 0.082 ns | 0.123 ns | 1.00 | - | 1 | 0.0190 | 248 B | 1.00 |
| MediatR_Send_Object | 46.14 ns | 0.332 ns | 0.496 ns | 1.08 | - | 2 | 0.0220 | 288 B | 1.16 |
| | | | | | | | | | |
| MediatorSG_Send_Generic | 11.13 ns | 0.024 ns | 0.035 ns | 1.00 | - | 1 | - | - | NA |
| MediatorSG_Send_Object | 13.51 ns | 0.096 ns | 0.140 ns | 1.21 | - | 2 | 0.0018 | 24 B | NA |

## Publish - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Publish | 1.460 ns | 0.0037 ns | 0.0056 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish | 2.359 ns | 0.0043 ns | 0.0065 ns | 1.62 | - | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 1.469 ns | 0.0031 ns | 0.0046 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatR_Publish | 112.916 ns | 0.1734 ns | 0.2595 ns | 76.86 | 0.29 | 2 | 0.0575 | 752 B | NA |
| | | | | | | | | | |
| Direct_Publish | 1.467 ns | 0.0015 ns | 0.0022 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish | 32.301 ns | 0.0683 ns | 0.1022 ns | 22.02 | 0.08 | 2 | - | - | NA |
| | | | | | | | | | |
| Direct_Publish | 1.454 ns | 0.0016 ns | 0.0023 ns | 1.00 | 0.00 | 1 | - | - | NA |
| MediatorSG_Publish | 6.319 ns | 0.0153 ns | 0.0215 ns | 4.35 | 0.02 | 2 | - | - | NA |

## Publish (Object) - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Publish_Generic | 2.407 ns | 0.0037 ns | 0.0053 ns | 1.00 | - | 1 | - | - | NA |
| DSoft_Publish_Object | 3.753 ns | 0.0035 ns | 0.0050 ns | 1.56 | - | 2 | - | - | NA |
| | | | | | | | | | |
| MediatR_Publish_Object | 111.6 ns | 0.31 ns | 0.46 ns | 0.94 | - | 1 | 0.0575 | 752 B | 1.00 |
| MediatR_Publish_Generic | 118.4 ns | 0.36 ns | 0.53 ns | 1.00 | - | 2 | 0.0575 | 752 B | 1.00 |
| | | | | | | | | | |
| DispatchR_Publish_Generic | 33.43 ns | 0.066 ns | 0.095 ns | 1.00 | 0.00 | 1 | - | - | NA |
| DispatchR_Publish_Object | 201.09 ns | 0.305 ns | 0.447 ns | 6.02 | 0.02 | 2 | 0.0196 | 256 B | NA |
| | | | | | | | | | |
| MediatorSG_Publish_Object | 4.076 ns | 0.0048 ns | 0.0072 ns | 0.65 | - | 1 | - | - | NA |
| MediatorSG_Publish_Generic | 6.261 ns | 0.0124 ns | 0.0185 ns | 1.00 | - | 2 | - | - | NA |

## Stream - All Libraries

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Direct_Stream | 30.12 ns | 0.069 ns | 0.103 ns | 30.11 ns | 1.00 | - | 1 | 0.0067 | 88 B | 1.00 |
| DSoft_Stream | 30.72 ns | 0.138 ns | 0.207 ns | 30.61 ns | 1.02 | - | 1 | 0.0067 | 88 B | 1.00 |
| | | | | | | | | | | |
| Direct_Stream | 30.94 ns | 0.085 ns | 0.120 ns | 30.94 ns | 1.00 | 0.00 | 1 | 0.0067 | 88 B | 1.00 |
| MediatR_Stream | 112.76 ns | 1.507 ns | 2.209 ns | 112.53 ns | 3.64 | 0.07 | 2 | 0.0354 | 464 B | 5.27 |
| | | | | | | | | | | |
| Direct_Stream | 31.15 ns | 0.084 ns | 0.121 ns | 31.16 ns | 1.00 | - | 1 | 0.0067 | 88 B | 1.00 |
| DispatchR_Stream | 54.02 ns | 0.069 ns | 0.103 ns | 54.01 ns | 1.73 | - | 2 | 0.0067 | 88 B | 1.00 |
| | | | | | | | | | | |
| Direct_Stream | 31.42 ns | 0.079 ns | 0.116 ns | 31.43 ns | 1.00 | - | 1 | 0.0067 | 88 B | 1.00 |
| MediatorSG_Stream | 31.78 ns | 0.157 ns | 0.230 ns | 31.81 ns | 1.01 | - | 1 | 0.0067 | 88 B | 1.00 |

## Concurrency - All Libraries

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_FanOut | 1,329.28 ns | 3.984 ns | 5.963 ns | - | 0.97 | 0.01 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_FanOut | 1,374.96 ns | 4.174 ns | 6.119 ns | - | 1.00 | 0.00 | 2 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| Direct_Throughput | 36.49 ns | 0.778 ns | 1.165 ns | - | 1.00 | 0.00 | 1 | - | - | - | NA |
| DSoft_Throughput | 45.72 ns | 0.095 ns | 0.140 ns | - | 1.25 | 0.04 | 2 | - | - | - | NA |
| | | | | | | | | | | | |
| Direct_FanOut | 1,338.54 ns | 4.723 ns | 7.070 ns | 1,340.10 ns | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatR_FanOut | 4,600.43 ns | 11.059 ns | 16.552 ns | 4,598.66 ns | 3.44 | 0.02 | 2 | 1.5640 | 0.0381 | 20536 B | 2.41 |
| Direct_Throughput | 35.80 ns | 0.711 ns | 1.065 ns | 35.20 ns | 1.00 | 0.00 | 1 | - | - | - | NA |
| MediatR_Throughput | 3,910.66 ns | 7.277 ns | 10.666 ns | 3,911.42 ns | 109.34 | 3.13 | 2 | 1.8921 | - | 24800 B | NA |
| | | | | | | | | | | | |
| Direct_FanOut | 1,304.42 ns | 11.290 ns | 16.548 ns | - | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| DispatchR_FanOut | 3,693.85 ns | 6.257 ns | 8.974 ns | - | 2.83 | 0.04 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 34.66 ns | 0.089 ns | 0.127 ns | - | 1.00 | 0.00 | 1 | - | - | - | NA |
| DispatchR_Throughput | 2,559.92 ns | 9.243 ns | 13.835 ns | - | 73.86 | 0.47 | 2 | - | - | - | NA |
| | | | | | | | | | | | |
| Direct_FanOut | 1,318.31 ns | 3.763 ns | 5.516 ns | - | 1.00 | 0.00 | 1 | 0.6523 | 0.0172 | 8536 B | 1.00 |
| MediatorSG_FanOut | 2,028.31 ns | 3.160 ns | 4.730 ns | - | 1.54 | 0.01 | 2 | 0.6523 | 0.0153 | 8536 B | 1.00 |
| Direct_Throughput | 36.22 ns | 0.774 ns | 1.135 ns | - | 1.00 | 0.00 | 1 | - | - | - | NA |
| MediatorSG_Throughput | 782.16 ns | 6.181 ns | 9.251 ns | - | 21.62 | 0.76 | 2 | - | - | - | NA |

## Cold Start - All Libraries

> **Three rows, each a superset of the one above.** `Startup_DiFloor` registers one trivial service
> in a container, builds it and resolves it — no mediator involved, the floor every library pays and
> none of them causes. `Startup_Registered` instead registers the library and resolves the mediator.
> `Startup_FirstRequest` adds the first dispatch.
>
> So `Registered - DiFloor` is what standing the library up costs, `FirstRequest - Registered` is the
> first dispatch, and **`FirstRequest - DiFloor` is everything the library adds to
> time-to-first-request** — the number an application actually feels.
>
> The floor resolves rather than merely building, and that matters more than it looks: on a container
> holding one trivial service, building took 6.80 ms and the first resolve another 5.44 ms. A
> baseline that only built would leave those 5.44 ms to be charged to whichever library the row
> belongs to. Registration is inside the measurement for the same reason — it used to sit in setup,
> which left it out of every row and pre-compiled the machinery the measured row then reused.
>
> Measured one process per sample, because startup is a property of a process and cannot be observed
> from inside a warm one. Process timings are skewed, so read the median rather than the mean.

| Method | Mean | Error | StdDev | Median | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DSoft_Startup_DiFloor | 11.67 ms | 0.121 ms | 0.215 ms | 11.64 ms | 1.00 | 0.00 | 1 | 7.42 KB | 1.00 |
| DSoft_Startup_Registered | 30.51 ms | 0.230 ms | 0.408 ms | 30.42 ms | 2.61 | 0.06 | 2 | 28.4 KB | 3.83 |
| DSoft_Startup_FirstRequest | 34.77 ms | 0.650 ms | 1.156 ms | 34.37 ms | 2.98 | 0.11 | 3 | 32.48 KB | 4.38 |
| | | | | | | | | | |
| MediatR_Startup_DiFloor | 11.65 ms | 0.114 ms | 0.203 ms | 11.63 ms | 1.00 | 0.00 | 1 | 7.42 KB | 1.00 |
| MediatR_Startup_Registered | 42.77 ms | 3.811 ms | 6.773 ms | 41.69 ms | 3.67 | 0.58 | 2 | 1624.42 KB | 218.87 |
| MediatR_Startup_FirstRequest | 43.20 ms | 0.208 ms | 0.371 ms | 43.14 ms | 3.71 | 0.07 | 3 | 1680.12 KB | 226.37 |
| | | | | | | | | | |
| DispatchR_Startup_DiFloor | 13.51 ms | 5.522 ms | 9.816 ms | 11.95 ms | 1.00 | 0.00 | 1 | 19.47 KB | 1.00 |
| DispatchR_Startup_FirstRequest | 24.14 ms | 0.209 ms | 0.372 ms | 24.08 ms | 1.98 | 0.27 | 2 | 650.93 KB | 33.43 |
| DispatchR_Startup_Registered | 24.48 ms | 9.630 ms | 17.117 ms | 21.75 ms | 2.01 | 1.42 | 3 | 648.85 KB | 33.33 |
| | | | | | | | | | |
| MediatorSG_Startup_DiFloor | 11.63 ms | 0.107 ms | 0.191 ms | 11.65 ms | 1.00 | 0.00 | 1 | 7.42 KB | 1.00 |
| MediatorSG_Startup_Registered | 14.74 ms | 0.123 ms | 0.219 ms | 14.74 ms | 1.27 | 0.03 | 2 | 22.2 KB | 2.99 |
| MediatorSG_Startup_FirstRequest | 21.90 ms | 0.221 ms | 0.394 ms | 21.81 ms | 1.88 | 0.05 | 3 | 153.76 KB | 20.72 |

## Realistic Pipeline - All Libraries

| Method | Mean | Error | StdDev | Ratio | RatioSD | Rank | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| DirectCall_WithPipeline | 100.1 ns | 0.16 ns | 0.23 ns | 1.00 | - | 1 | 0.0129 | 168 B | 1.00 |
| DSoft_RealisticPipeline | 111.9 ns | 0.52 ns | 0.70 ns | 1.12 | - | 2 | 0.0110 | 144 B | 0.86 |
| | | | | | | | | | |
| DirectCall_WithPipeline | 98.57 ns | 0.345 ns | 0.495 ns | 1.00 | 0.00 | 1 | 0.0129 | 168 B | 1.00 |
| MediatR_RealisticPipeline | 350.62 ns | 0.585 ns | 0.781 ns | 3.56 | 0.02 | 2 | 0.0777 | 1016 B | 6.05 |
| | | | | | | | | | |
| DirectCall_WithPipeline | 99.99 ns | 0.250 ns | 0.366 ns | 1.00 | 0.00 | 1 | 0.0129 | 168 B | 1.00 |
| DispatchR_RealisticPipeline | 246.92 ns | 1.630 ns | 2.176 ns | 2.47 | 0.02 | 2 | 0.0200 | 263 B | 1.57 |
| | | | | | | | | | |
| DirectCall_WithPipeline | 96.71 ns | 0.190 ns | 0.260 ns | 1.00 | - | 1 | 0.0129 | 168 B | 1.00 |
| MediatorSG_RealisticPipeline | 237.69 ns | 0.760 ns | 1.138 ns | 2.46 | - | 2 | 0.0255 | 335 B | 1.99 |

## Running Benchmarks

Close Visual Studio and heavy apps before running for best accuracy.

```sh
# All benchmarks sequentially (recommended)
benchmarks\run-all-benchmarks.cmd
```

Results are saved to `benchmarks/BenchmarkDotNet.Artifacts/<tfm>/results/`.
