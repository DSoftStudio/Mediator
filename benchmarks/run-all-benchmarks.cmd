@echo off
echo ============================================================
echo  DSoftStudio.Mediator - Full Benchmark Suite (Isolated)
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

set PROJECT=DSoftStudio.Mediator.Benchmarks
set CMD=dotnet run --project %PROJECT% -c Release --

echo [1/10] DSoft Send - No Behaviors
%CMD% --filter "*DSoftSendNoBehaviors*"

echo [2/10] DSoft Send - 3 ^& 5 Behaviors
%CMD% --filter "*DSoftSendBenchmarks*"

echo [3/10] MediatR Send - No Behaviors
%CMD% --filter "*MediatRSendNoBehaviors*"

echo [4/10] MediatR Send - 3 ^& 5 Behaviors
%CMD% --filter "*MediatRSendBenchmarks*"

echo [5/10] DispatchR Send - No Behaviors
%CMD% --filter "*DispatchRSendNoBehaviors*"

echo [6/10] DispatchR Send - 3 ^& 5 Behaviors
%CMD% --filter "*DispatchRSendBenchmarks*"

echo [7/10] Publish
%CMD% --filter "*PublishBenchmarks*"

echo [8/10] Stream
%CMD% --filter "*StreamBenchmarks*"

echo [9/10] Concurrency
%CMD% --filter "*ConcurrencyBenchmarks*"

echo [10/10] ColdStart
%CMD% --filter "*ColdStartBenchmarks*"

echo.
echo ============================================================
echo  Generating BENCHMARKS.md ...
echo ============================================================
powershell -ExecutionPolicy Bypass -File "%~dp0generate-benchmarks-md.ps1"

echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\results\
echo  Summary: benchmarks\BENCHMARKS.md
echo ============================================================
pause
