@echo off
echo === Poe Ancients Price Helper - Build ^& Run ===
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] dotnet SDK not found. Install from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Building...
dotnet publish src/PoeAncientsPriceHelper/PoeAncientsPriceHelper.csproj -c Release -r win-x64 --self-contained false -o publish
if %errorlevel% neq 0 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo Starting application...
start "" "publish\PoeAncientsPriceHelper.exe"
