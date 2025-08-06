@echo off
echo Building and starting CursorPhobia in System Tray...

:: Always build to ensure we have latest changes
dotnet build src\Console\CursorPhobia.Console.csproj --configuration Release --verbosity minimal
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

:: Launch directly in tray mode (no console window will show)
"src\Console\bin\Release\net8.0-windows\CursorPhobia.Console.exe" --tray

echo Done!