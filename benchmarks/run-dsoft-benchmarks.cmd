@echo off
setlocal
rem Usage: run-dsoft-benchmarks.cmd [net10.0^|net11.0^|all]   (default: all)
set "TFMS=%~1"
if "%TFMS%"=="" set "TFMS=all"
if /i "%TFMS%"=="all" (set "LIST=net10.0 net11.0") else (set "LIST=%TFMS%")

echo ============================================================
echo  DSoftStudio.Mediator - DSoft Benchmark Suite  [%TFMS%]
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
echo  DSoft (Isolated) on %~1
echo ============================================================

echo [1/9] DSoft - Send (No Behaviors) [%~1]
%CMD% --filter "Benchmarks.DSoftSendNoBehaviorsBenchmarks.*"

echo [2/9] DSoft - Send (Behaviors) [%~1]
%CMD% --filter "Benchmarks.DSoftSendBenchmarks.*"

echo [3/9] DSoft - Send (Object) [%~1]
%CMD% --filter "Benchmarks.DSoftSendObjectBenchmarks.*"

echo [4/9] DSoft - Publish [%~1]
%CMD% --filter "Benchmarks.DSoftPublishBenchmarks.*"

echo [5/9] DSoft - Publish (Object) [%~1]
%CMD% --filter "Benchmarks.DSoftPublishObjectBenchmarks.*"

echo [6/9] DSoft - Stream [%~1]
%CMD% --filter "Benchmarks.DSoftStreamBenchmarks.*"

echo [7/9] DSoft - Concurrency [%~1]
%CMD% --filter "Benchmarks.DSoftConcurrencyBenchmarks.*"

echo [8/9] DSoft - Cold Start [%~1]
%CMD% --filter "Benchmarks.DSoftColdStartBenchmarks.*"

echo [9/9] DSoft - Realistic Pipeline [%~1]
%CMD% --filter "Benchmarks.DSoftRealisticPipelineBenchmarks.*"

popd
exit /b 0
