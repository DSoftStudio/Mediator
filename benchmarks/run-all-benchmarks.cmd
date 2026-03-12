@echo off
echo ============================================================
echo  DSoftStudio.Mediator - Full Benchmark Suite (Isolated)
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

set PROJECT=DSoftStudio.Mediator.Benchmarks
set CMD=dotnet run --project %PROJECT% -c Release --

echo [1/14] DSoft Send - No Behaviors
%CMD% --filter "*DSoftSendNoBehaviors*"

echo [2/14] DSoft Send - 3 ^& 5 Behaviors
%CMD% --filter "*DSoftSendBenchmarks*"

echo [3/14] MediatR Send - No Behaviors
%CMD% --filter "*MediatRSendNoBehaviors*"

echo [4/14] MediatR Send - 3 ^& 5 Behaviors
%CMD% --filter "*MediatRSendBenchmarks*"

echo [5/14] DispatchR Send - No Behaviors
%CMD% --filter "*DispatchRSendNoBehaviors*"

echo [6/14] DispatchR Send - 3 ^& 5 Behaviors
%CMD% --filter "*DispatchRSendBenchmarks*"

echo [7/14] MediatorSG Send - No Behaviors
%CMD% --filter "*MediatorSGSendNoBehaviors*"

echo [8/14] MediatorSG Send - 3 ^& 5 Behaviors
%CMD% --filter "*MediatorSGSendBenchmarks*"

echo [9/14] Send - All Libraries (No Behaviors)
%CMD% --filter "Benchmarks.SendNoBehaviorsBenchmarks*"

echo [10/14] Send - All Libraries (Behaviors)
%CMD% --filter "Benchmarks.SendBenchmarks*"

echo [11/14] Publish
%CMD% --filter "*PublishBenchmarks*"

echo [12/14] Stream
%CMD% --filter "*StreamBenchmarks*"

echo [13/14] Concurrency
%CMD% --filter "*ConcurrencyBenchmarks*"

echo [14/14] ColdStart
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
