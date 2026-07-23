@echo off
setlocal
rem Usage: run-dispatchr-benchmarks.cmd [net10.0^|net11.0^|all]   (default: all)
set "TFMS=%~1"
if "%TFMS%"=="" set "TFMS=all"
if /i "%TFMS%"=="all" (set "LIST=net10.0 net11.0") else (set "LIST=%TFMS%")

echo ============================================================
echo  DSoftStudio.Mediator - DispatchR Benchmark Suite  [%TFMS%]
echo  Close VS and other heavy apps before running.
echo ============================================================

for %%F in (%LIST%) do call :run_tfm %%F

echo.
echo ============================================================
echo  All benchmarks complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\^<tfm^>\results\
echo  Summary: benchmarks\BENCHMARKS.md
echo ============================================================
if not defined DSOFT_BENCH_NO_PAUSE pause
exit /b 0

:run_tfm
pushd "%~dp0"
set CMD=dotnet run --project DSoftStudio.Mediator.Benchmarks -c Release -f %~1 --

echo.
echo ============================================================
echo  DispatchR (Isolated) on %~1
echo ============================================================

echo [1/8] DispatchR - Send (No Behaviors) [%~1]
%CMD% --filter "Benchmarks.DispatchRSendNoBehaviorsBenchmarks.*"

echo [2/8] DispatchR - Send (Behaviors) [%~1]
%CMD% --filter "Benchmarks.DispatchRSendBenchmarks.*"

echo [3/8] DispatchR - Publish [%~1]
%CMD% --filter "Benchmarks.DispatchRPublishBenchmarks.*"

echo [4/8] DispatchR - Publish (Object) [%~1]
%CMD% --filter "Benchmarks.DispatchRPublishObjectBenchmarks.*"

echo [5/8] DispatchR - Stream [%~1]
%CMD% --filter "Benchmarks.DispatchRStreamBenchmarks.*"

echo [6/8] DispatchR - Concurrency [%~1]
%CMD% --filter "Benchmarks.DispatchRConcurrencyBenchmarks.*"

echo [7/8] DispatchR - Cold Start [%~1]
%CMD% --filter "Benchmarks.DispatchRColdStartBenchmarks.*"

echo [8/8] DispatchR - Realistic Pipeline [%~1]
%CMD% --filter "Benchmarks.DispatchRRealisticPipelineBenchmarks.*"

popd
exit /b 0
