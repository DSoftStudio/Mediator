@echo off
setlocal
rem Usage: run-mediatr-benchmarks.cmd [net10.0^|net11.0^|all]   (default: all)
set "TFMS=%~1"
if "%TFMS%"=="" set "TFMS=all"
if /i "%TFMS%"=="all" (set "LIST=net10.0 net11.0") else (set "LIST=%TFMS%")

echo ============================================================
echo  DSoftStudio.Mediator - MediatR Benchmark Suite  [%TFMS%]
echo  Close VS and other heavy apps before running.
echo ============================================================

for %%F in (%LIST%) do call :run_tfm %%F

echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\^<tfm^>\results\
echo ============================================================
if not defined DSOFT_BENCH_NO_PAUSE pause
exit /b 0

:run_tfm
pushd "%~dp0"
set CMD=dotnet run --project DSoftStudio.Mediator.Benchmarks -c Release -f %~1 --

echo.
echo ============================================================
echo  MediatR (Isolated) on %~1
echo ============================================================

echo [1/10] MediatR - Send (No Behaviors) [%~1]
%CMD% --filter "Benchmarks.MediatRSendNoBehaviorsBenchmarks.*"

echo [2/10] MediatR - Send (Behaviors) [%~1]
%CMD% --filter "Benchmarks.MediatRSendBenchmarks.*"

echo [3/10] MediatR - Send (Object) [%~1]
%CMD% --filter "Benchmarks.MediatRSendObjectBenchmarks.*"

echo [4/10] MediatR - Publish [%~1]
%CMD% --filter "Benchmarks.MediatRPublishBenchmarks.*"

echo [5/10] MediatR - Publish (Object) [%~1]
%CMD% --filter "Benchmarks.MediatRPublishObjectBenchmarks.*"

echo [6/10] MediatR - Stream [%~1]
%CMD% --filter "Benchmarks.MediatRStreamBenchmarks.*"

echo [7/10] MediatR - Concurrency [%~1]
%CMD% --filter "Benchmarks.MediatRConcurrencyBenchmarks.*"

echo [8/10] MediatR - Cold Start [%~1]
%CMD% --filter "Benchmarks.MediatRColdStartBenchmarks.*"

echo [9/10] MediatR - Realistic Pipeline [%~1]
%CMD% --filter "Benchmarks.MediatRRealisticPipelineBenchmarks.*"

echo [10/10] MediatR - Behavior Scaling [%~1]
%CMD% --filter "Benchmarks.MediatRBehaviorScalingBenchmarks.*"

popd
exit /b 0
