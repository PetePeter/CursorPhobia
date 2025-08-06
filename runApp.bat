@echo off
echo CursorPhobia - Build and Run in System Tray
echo ===========================================

echo [1/2] Building application...
dotnet build src\Console\CursorPhobia.Console.csproj --configuration Release --verbosity minimal

if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED!
    dotnet build src\Console\CursorPhobia.Console.csproj --configuration Release --verbosity normal
    pause
    exit /b 1
)

echo [2/2] Starting CursorPhobia in system tray...
set "EXE_PATH=src\Console\bin\Release\net8.0-windows\CursorPhobia.Console.exe"

if not exist "%EXE_PATH%" (
    echo ERROR: Executable not found at %EXE_PATH%
    pause
    exit /b 1
)

echo.
echo ✓ Build completed successfully!
echo ✓ Starting CursorPhobia in system tray mode...
echo.

start "" "%EXE_PATH%" --tray

echo Waiting for application to initialize...
timeout /t 3 /nobreak >nul

tasklist /FI "IMAGENAME eq CursorPhobia.Console.exe" | find /I "CursorPhobia.Console.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo ✓ CursorPhobia is now running in the background!
    echo.
    echo Look for the CursorPhobia icon in your system tray ^(bottom-right corner^).
    echo Right-click the tray icon to enable/disable or access settings.
    echo The application starts with the engine DISABLED for safety.
    echo.
    echo This window can be closed - the app will continue running in the tray.
) else (
    echo ✗ CursorPhobia may not have started correctly.
    echo Check the logs in: %%APPDATA%%\CursorPhobia\Logs
)

echo.
echo Press any key to close this window...
pause >nul