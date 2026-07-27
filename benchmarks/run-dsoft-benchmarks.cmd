@echo off
echo ============================================================
echo  DSoftStudio.Mediator - Full Benchmark Suite
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

set PROJECT=DSoftStudio.Mediator.Benchmarks
set CMD=dotnet run --project %PROJECT% -c Release --

echo ============================================================
echo  DSoft (Isolated)
echo ============================================================

echo [1/9] DSoft - Send (No Behaviors)
%CMD% --filter "Benchmarks.DSoftSendNoBehaviorsBenchmarks.*"

echo [2/9] DSoft - Send (Behaviors)
%CMD% --filter "Benchmarks.DSoftSendBenchmarks.*"

echo [3/9] DSoft - Send (Object)
%CMD% --filter "Benchmarks.DSoftSendObjectBenchmarks.*"

echo [4/9] DSoft - Publish
%CMD% --filter "Benchmarks.DSoftPublishBenchmarks.*"

echo [5/9] DSoft - Publish (Object)
%CMD% --filter "Benchmarks.DSoftPublishObjectBenchmarks.*"

echo [6/9] DSoft - Stream
%CMD% --filter "Benchmarks.DSoftStreamBenchmarks.*"

echo [7/9] DSoft - Concurrency
%CMD% --filter "Benchmarks.DSoftConcurrencyBenchmarks.*"

echo [8/9] DSoft - Cold Start
%CMD% --filter "Benchmarks.DSoftColdStartBenchmarks.*"

echo [9/9] DSoft - Realistic Pipeline
%CMD% --filter "Benchmarks.DSoftRealisticPipelineBenchmarks.*"


echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\results\
echo ============================================================
if not defined DSOFT_BENCH_NO_PAUSE pause
