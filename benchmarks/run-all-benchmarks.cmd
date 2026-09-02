@echo off
setlocal
rem Usage: run-all-benchmarks.cmd [net10.0^|net11.0^|all]   (default: all)
rem
rem Each library is launched in its OWN process (once per TFM). That isolation is
rem deliberate: a single process would let one mediator's static dispatch tables
rem contaminate another's measurements. See BENCHMARKS.md and the per-suite
rem "No other mediator libraries" note in Benchmarks\*\*Benchmarks.cs.
set "TFMS=%~1"
if "%TFMS%"=="" set "TFMS=all"
set DSOFT_BENCH_NO_PAUSE=1
pushd "%~dp0"

echo ============================================================
echo  DSoftStudio.Mediator - All Library Benchmark Suites  [%TFMS%]
echo  DSoft, MediatR, Mediator (Source Gen), DispatchR - run
echo  sequentially, each library isolated in its own process.
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

call "%~dp0run-dsoft-benchmarks.cmd" %TFMS%
call "%~dp0run-mediatr-benchmarks.cmd" %TFMS%
call "%~dp0run-mediator-sg-benchmarks.cmd" %TFMS%
call "%~dp0run-dispatchr-benchmarks.cmd" %TFMS%

rem Regenerate the summary, once per TFM. generate-benchmarks-md.ps1 says it is called from here
rem and it no longer was, so BENCHMARKS.md kept describing whatever run last touched the old flat
rem artifacts directory.
if /i "%TFMS%"=="all" (set "GENLIST=net10.0 net11.0") else (set "GENLIST=%TFMS%")
for %%G in (%GENLIST%) do (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0generate-benchmarks-md.ps1" -Tfm %%G
)

popd
echo.
echo ============================================================
echo  All library suites complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\^<tfm^>\results\
echo  Summary: benchmarks\BENCHMARKS.md
echo ============================================================
endlocal
pause
