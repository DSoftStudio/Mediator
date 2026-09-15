@echo off
setlocal
rem Usage: run-mediator-sg-benchmarks.cmd [net10.0^|net11.0^|all]   (default: all)
set "TFMS=%~1"
if "%TFMS%"=="" set "TFMS=all"
if /i "%TFMS%"=="all" (set "LIST=net10.0 net11.0") else (set "LIST=%TFMS%")

echo ============================================================
echo  DSoftStudio.Mediator - Mediator Source Gen Benchmark Suite  [%TFMS%]
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
echo  Mediator Source Gen (Isolated) on %~1
echo ============================================================

echo [1/10] Mediator (Source Gen) - Send (No Behaviors) [%~1]
%CMD% --filter "Benchmarks.MediatorSGSendNoBehaviorsBenchmarks.*"

echo [2/10] Mediator (Source Gen) - Send (Behaviors) [%~1]
%CMD% --filter "Benchmarks.MediatorSGSendBenchmarks.*"

echo [3/10] Mediator (Source Gen) - Send (Object) [%~1]
%CMD% --filter "Benchmarks.MediatorSGSendObjectBenchmarks.*"

echo [4/10] Mediator (Source Gen) - Publish [%~1]
%CMD% --filter "Benchmarks.MediatorSGPublishBenchmarks.*"

echo [5/10] Mediator (Source Gen) - Publish (Object) [%~1]
%CMD% --filter "Benchmarks.MediatorSGPublishObjectBenchmarks.*"

echo [6/10] Mediator (Source Gen) - Stream [%~1]
%CMD% --filter "Benchmarks.MediatorSGStreamBenchmarks.*"

echo [7/10] Mediator (Source Gen) - Concurrency [%~1]
%CMD% --filter "Benchmarks.MediatorSGConcurrencyBenchmarks.*"

echo [8/10] Mediator (Source Gen) - Cold Start [%~1]
%CMD% --filter "Benchmarks.MediatorSGColdStartBenchmarks.*"

echo [9/10] Mediator (Source Gen) - Realistic Pipeline [%~1]
%CMD% --filter "Benchmarks.MediatorSGRealisticPipelineBenchmarks.*"

echo [10/10] Mediator (Source Gen) - Behavior Scaling [%~1]
%CMD% --filter "Benchmarks.MediatorSGBehaviorScalingBenchmarks.*"

popd
exit /b 0
