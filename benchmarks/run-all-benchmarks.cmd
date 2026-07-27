@echo off
setlocal
set DSOFT_BENCH_NO_PAUSE=1
pushd "%~dp0"

echo ============================================================
echo  DSoftStudio.Mediator - All Library Benchmark Suites
echo  DSoft, MediatR, Mediator (Source Gen), DispatchR - run
echo  sequentially, each library isolated in its own process.
echo  Close VS and other heavy apps before running.
echo ============================================================
echo.

call "%~dp0run-dsoft-benchmarks.cmd"
call "%~dp0run-mediatr-benchmarks.cmd"
call "%~dp0run-mediator-sg-benchmarks.cmd"
call "%~dp0run-dispatchr-benchmarks.cmd"

popd
echo.
echo ============================================================
echo  All library suites complete!
echo  Results: benchmarks\BenchmarkDotNet.Artifacts\results\
echo  Summary: benchmarks\BENCHMARKS.md
echo ============================================================
endlocal
pause
