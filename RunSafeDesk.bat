@echo off
setlocal

echo [SafeDesk] Checking environment...

:: Check if dotnet exists
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] .NET is not found in your PATH.
    echo Please install the .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

:: Check for SDKs
for /f "tokens=*" %%i in ('dotnet --list-sdks') do set SDK_FOUND=%%i
if "%SDK_FOUND%"=="" (
    echo [ERROR] No .NET SDKs were found. You only have the Runtime installed.
    echo You specifically need the .NET 8.0 SDK to build and run this application.
    echo.
    echo Current dotnet version:
    dotnet --version
    echo.
    echo Please download "PC -> Install .NET SDK" from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [SUCCESS] .NET SDK found. Building and running SafeDesk...
dotnet run --project SafeDesk.UI

if %errorlevel% neq 0 (
    echo [ERROR] Application crashed or failed to build.
    pause
)
