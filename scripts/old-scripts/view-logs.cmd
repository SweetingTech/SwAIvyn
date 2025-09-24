@echo off
REM SwAIvyn Log Viewer
REM This script helps view log files from any directory

setlocal enabledelayedexpansion

set PARAMS=

:parse
if "%~1"=="" goto endparse
if /i "%~1"=="-ShowCrashLogs" (
    set PARAMS=!PARAMS! -ShowCrashLogs
) else if /i "%~1"=="-ShowLatest:$false" (
    set PARAMS=!PARAMS! -ShowLatest:$false
) else if /i "%~1"=="-ShowLatest:false" (
    set PARAMS=!PARAMS! -ShowLatest:$false
) else (
    set PARAMS=!PARAMS! %1
)
shift
goto parse
:endparse

powershell -ExecutionPolicy Bypass -File "%~dp0scripts\view-logs.ps1" %PARAMS%
