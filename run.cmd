@echo off
echo Building and running SwAIvyn...
cd backend
dotnet build
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)
cd ..
echo Starting SwAIvyn...
SwAIvyn.exe
