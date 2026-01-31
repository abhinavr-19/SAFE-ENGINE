@echo off
setlocal

echo [SafeDesk] Checking environment...

:: 1. Check if dotnet is specifically in Program Files (Preferred)
if exist "C:\Program Files\dotnet\dotnet.exe" (
    echo [INFO] Found .NET at C:\Program Files\dotnet
    set "DOTNET_ROOT=C:\Program Files\dotnet"
    goto FOUND
)

:: 2. Check if dotnet is in x86 Program Files
if exist "C:\Program Files (x86)\dotnet\dotnet.exe" (
    echo [INFO] Found .NET at C:\Program Files (x86)\dotnet
    set "DOTNET_ROOT=C:\Program Files (x86)\dotnet"
    goto FOUND
)

:: 3. Check if dotnet is just in the PATH
where dotnet >nul 2>nul
if %errorlevel% equ 0 (
    echo [INFO] .NET found in PATH.
    goto FOUND_IN_PATH
)

echo [ERROR] .NET is not found in your PATH or standard locations.
echo Please install the .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
pause
exit /b 1

:FOUND
:: Add to PATH safely outside of IF blocks to avoid syntax errors with parentheses
set "PATH=%DOTNET_ROOT%;%PATH%"

:FOUND_IN_PATH
echo [SUCCESS] Launching SafeDesk...
dotnet run --project SafeDesk.UI

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Application crashed or failed to launch.
    echo If it says "not recognized", the SDK might be missing.
    pause
)
