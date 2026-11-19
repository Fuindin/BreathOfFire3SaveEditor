@echo off
rem Builds the editor and writes the compiler output to build.log.
cd /d "%~dp0"
dotnet build -c Debug > build.log 2>&1
echo EXITCODE %ERRORLEVEL% >> build.log
