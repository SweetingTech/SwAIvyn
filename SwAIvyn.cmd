@echo off
REM SwAIvyn Launcher
REM This script launches the SwAIvyn application

title SwAIvyn Launcher

echo ===================================
echo       SwAIvyn AI Assistant
echo ===================================
echo.

REM Check if the executable exists
if not exist "%~dp0dist\SwAIvyn.exe" (
    echo SwAIvyn executable not found. Building the application...
    echo This may take a few minutes for the first run.
    echo.

    powershell -ExecutionPolicy Bypass -File "%~dp0scripts\build-app.ps1"
    if errorlevel 1 (
        echo.
        echo Build failed. Please check the error messages above.
        echo.
        echo Press any key to exit...
        pause > nul
        exit /b 1
    )

    echo.
    echo Build completed successfully!
)

echo.
echo Starting SwAIvyn...
echo The application will open in your default browser.
echo.
echo To close the application, close this window or press Ctrl+C.
echo.

REM Launch the application
start "" "%~dp0dist\SwAIvyn.exe"

echo SwAIvyn is running. Do not close this window while using the application.
echo.

REM Keep the window open
pause > nul
