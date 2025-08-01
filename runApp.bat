@echo off
echo ===============================================
echo CursorPhobia - Build and Run System Tray App
echo ===============================================
echo.

:: Check if .NET 8 is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Checking .NET version...
for /f "tokens=1" %%i in ('dotnet --version') do set NET_VERSION=%%i
echo Found .NET version: %NET_VERSION%
echo.

:: Clean previous builds
echo Cleaning previous builds...
dotnet clean src\Console\CursorPhobia.Console.csproj --configuration Release --verbosity minimal
if errorlevel 1 (
    echo ERROR: Failed to clean project
    pause
    exit /b 1
)
echo.

:: Build the application in Release mode
echo Building CursorPhobia in Release mode...
dotnet build src\Console\CursorPhobia.Console.csproj --configuration Release --verbosity minimal
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)
echo Build completed successfully!
echo.

:: Create single-file executable (optional - uncomment if you want a single EXE)
:: echo Creating single-file executable...
:: dotnet publish src\Console\CursorPhobia.Console.csproj --configuration Release --runtime win-x64 --self-contained true --output bin\Release\publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --verbosity minimal

:: Find the built executable
set "EXE_PATH=src\Console\bin\Release\net8.0-windows\CursorPhobia.Console.exe"
if not exist "%EXE_PATH%" (
    echo ERROR: Built executable not found at %EXE_PATH%
    echo Build may have failed or executable is in a different location
    pause
    exit /b 1
)

echo Found executable at: %EXE_PATH%
echo.

:: Show launch options
echo CursorPhobia is ready to run!
echo.
echo Choose launch mode:
echo [1] System Tray Mode (recommended) - Runs in background with tray icon
echo [2] Console Demo Mode - Interactive console with test options
echo [3] Exit
echo.
set /p choice="Enter your choice (1-3): "

if "%choice%"=="1" goto tray_mode
if "%choice%"=="2" goto console_mode
if "%choice%"=="3" goto exit
echo Invalid choice, defaulting to System Tray Mode
goto tray_mode

:tray_mode
echo.
echo Starting CursorPhobia in System Tray Mode...
echo ============================================
echo.
echo CursorPhobia will now run in the system tray.
echo Look for the CursorPhobia icon in your system tray (bottom-right corner).
echo Right-click the tray icon to:
echo   - Enable/Disable the cursor phobia engine
echo   - Access settings (Phase B - coming soon)
echo   - View about information
echo   - Exit the application
echo.
echo The application will start with the engine DISABLED for safety.
echo Use the tray menu to enable it when ready.
echo.
echo Starting application...
start "" "%EXE_PATH%" --tray
echo.
echo CursorPhobia has been launched in the background.
echo Check your system tray for the application icon.
echo.
echo This window can be closed - the application will continue running.
pause
goto exit

:console_mode
echo.
echo Starting CursorPhobia in Console Demo Mode...
echo =============================================
echo.
echo WARNING: Console mode includes interactive demos that will actively
echo          push windows away from your cursor! Use CTRL key to disable.
echo.
start "" "%EXE_PATH%"
echo.
echo Console demo launched in a new window.
pause
goto exit

:exit
echo.
echo Thank you for using CursorPhobia!
echo.
:: Small delay so user can see the message
timeout /t 2 /nobreak >nul
exit /b 0