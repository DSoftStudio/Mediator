@echo off
echo ============================================================
echo  DSoftStudio.Mediator - Full Benchmark Suite
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

set PROJECT=DSoftStudio.Mediator.Benchmarks
set CMD=dotnet run --project %PROJECT% -c Release --

echo ============================================================
echo  DispatchR (Isolated)
echo ============================================================

echo [1/8] DispatchR - Send (No Behaviors)
%CMD% --filter "Benchmarks.DispatchRSendNoBehaviorsBenchmarks.*"

echo [2/8] DispatchR - Send (Behaviors)
%CMD% --filter "Benchmarks.DispatchRSendBenchmarks.*"

echo [3/8] DispatchR - Publish
%CMD% --filter "Benchmarks.DispatchRPublishBenchmarks.*"

echo [4/8] DispatchR - Publish (Object)
%CMD% --filter "Benchmarks.DispatchRPublishObjectBenchmarks.*"

echo [5/8] DispatchR - Stream
%CMD% --filter "Benchmarks.DispatchRStreamBenchmarks.*"

echo [6/8] DispatchR - Concurrency
%CMD% --filter "Benchmarks.DispatchRConcurrencyBenchmarks.*"

echo [7/8] DispatchR - Cold Start
%CMD% --filter "Benchmarks.DispatchRColdStartBenchmarks.*"

echo [8/8] DispatchR - Realistic Pipeline
%CMD% --filter "Benchmarks.DispatchRRealisticPipelineBenchmarks.*"

echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\results\
echo  Summary: benchmarks\BENCHMARKS.md
echo ============================================================
if not defined DSOFT_BENCH_NO_PAUSE pause
