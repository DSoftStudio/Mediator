@echo off
echo ============================================================
echo  DSoftStudio.Mediator - Full Benchmark Suite
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

set PROJECT=DSoftStudio.Mediator.Benchmarks
set CMD=dotnet run --project %PROJECT% -c Release --

echo ============================================================
echo  Mediator Source Gen (Isolated)
echo ============================================================

echo [1/9] Mediator (Source Gen) - Send (No Behaviors)
%CMD% --filter "Benchmarks.MediatorSGSendNoBehaviorsBenchmarks.*"

echo [2/9] Mediator (Source Gen) - Send (Behaviors)
%CMD% --filter "Benchmarks.MediatorSGSendBenchmarks.*"

echo [3/9] Mediator (Source Gen) - Send (Object)
%CMD% --filter "Benchmarks.MediatorSGSendObjectBenchmarks.*"

echo [4/9] Mediator (Source Gen) - Publish
%CMD% --filter "Benchmarks.MediatorSGPublishBenchmarks.*"

echo [5/9] Mediator (Source Gen) - Publish (Object)
%CMD% --filter "Benchmarks.MediatorSGPublishObjectBenchmarks.*"

echo [6/9] Mediator (Source Gen) - Stream
%CMD% --filter "Benchmarks.MediatorSGStreamBenchmarks.*"

echo [7/9] Mediator (Source Gen) - Concurrency
%CMD% --filter "Benchmarks.MediatorSGConcurrencyBenchmarks.*"

echo [8/9] Mediator (Source Gen) - Cold Start
%CMD% --filter "Benchmarks.MediatorSGColdStartBenchmarks.*"

echo [9/9] Mediator (Source Gen) - Realistic Pipeline
%CMD% --filter "Benchmarks.MediatorSGRealisticPipelineBenchmarks.*"

echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\results\
echo ============================================================
if not defined DSOFT_BENCH_NO_PAUSE pause
