@echo off
echo ============================================================
echo  DSoftStudio.Mediator - Full Benchmark Suite
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

set PROJECT=DSoftStudio.Mediator.Benchmarks
set CMD=dotnet run --project %PROJECT% -c Release --

echo ============================================================
echo  MediatR (Isolated)
echo ============================================================

echo [1/9] MediatR - Send (No Behaviors)
%CMD% --filter "Benchmarks.MediatRSendNoBehaviorsBenchmarks.*"

echo [2/9] MediatR - Send (Behaviors)
%CMD% --filter "Benchmarks.MediatRSendBenchmarks.*"

echo [3/9] MediatR - Send (Object)
%CMD% --filter "Benchmarks.MediatRSendObjectBenchmarks.*"

echo [4/9] MediatR - Publish
%CMD% --filter "Benchmarks.MediatRPublishBenchmarks.*"

echo [5/9] MediatR - Publish (Object)
%CMD% --filter "Benchmarks.MediatRPublishObjectBenchmarks.*"

echo [6/9] MediatR - Stream
%CMD% --filter "Benchmarks.MediatRStreamBenchmarks.*"

echo [7/9] MediatR - Concurrency
%CMD% --filter "Benchmarks.MediatRConcurrencyBenchmarks.*"

echo [8/9] MediatR - Cold Start
%CMD% --filter "Benchmarks.MediatRColdStartBenchmarks.*"

echo [9/9] MediatR - Realistic Pipeline
%CMD% --filter "Benchmarks.MediatRRealisticPipelineBenchmarks.*"

echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\results\
echo ============================================================
if not defined DSOFT_BENCH_NO_PAUSE pause
