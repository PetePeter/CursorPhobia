@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM CursorPhobia Enhanced Build Script - Phase 3 WI#8
REM Automated build process with MSBuild integration, versioning, and packaging
REM ============================================================================

echo.
echo ========================================
echo CursorPhobia Enhanced Build Script
echo Phase 3 WI#8: Build Automation
echo ========================================
echo.

REM Configuration
set BUILD_CONFIG=Release
set DOTNET_VERSION=8.0
set SOLUTION_FILE=CursorPhobia.sln
set CONSOLE_PROJECT=src\Console\CursorPhobia.Console.csproj
set CORE_PROJECT=src\Core\CursorPhobia.Core.csproj
set TEST_PROJECT=tests\CursorPhobia.Tests.csproj
set BUILD_DIR=build
set PUBLISH_DIR=publish

REM Parse command line arguments
set RUN_TESTS=true
set SKIP_CLEAN=false
set BUILD_TYPE=all
set VERBOSE=false

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="--no-tests" set RUN_TESTS=false
if /i "%~1"=="--skip-clean" set SKIP_CLEAN=true
if /i "%~1"=="--debug" set BUILD_CONFIG=Debug
if /i "%~1"=="--x64-only" set BUILD_TYPE=x64
if /i "%~1"=="--x86-only" set BUILD_TYPE=x86
if /i "%~1"=="--verbose" set VERBOSE=true
if /i "%~1"=="--help" goto show_help
shift
goto parse_args
:end_parse

REM Show configuration
echo Build Configuration: %BUILD_CONFIG%
echo Run Tests: %RUN_TESTS%
echo Build Type: %BUILD_TYPE%
echo Skip Clean: %SKIP_CLEAN%
echo.

REM Check prerequisites
echo [1/10] Checking prerequisites...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Please install .NET %DOTNET_VERSION% SDK.
    goto error_exit
)

for /f "tokens=1 delims=." %%a in ('dotnet --version') do set DOTNET_MAJOR=%%a
if %DOTNET_MAJOR% LSS 8 (
    echo ERROR: .NET 8.0 or higher required. Found version: 
    dotnet --version
    goto error_exit
)

if not exist "%SOLUTION_FILE%" (
    echo ERROR: Solution file not found: %SOLUTION_FILE%
    goto error_exit
)

echo ✓ Prerequisites check passed
echo.

REM Generate version information
echo [2/10] Generating version information...
call :generate_version
echo ✓ Version information generated
echo   Version: %BUILD_VERSION%
echo   Build Number: %BUILD_NUMBER%
echo   Git Hash: %GIT_HASH%
echo   Build Date: %BUILD_DATE%
echo.

REM Clean previous builds
if "%SKIP_CLEAN%"=="false" (
    echo [3/10] Cleaning previous builds...
    if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
    if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
    
    REM Clean solution
    dotnet clean "%SOLUTION_FILE%" --configuration %BUILD_CONFIG% --verbosity minimal
    if errorlevel 1 goto error_exit
    
    echo ✓ Clean completed
) else (
    echo [3/10] Skipping clean (--skip-clean specified)
)
echo.

REM Create build directories
echo [4/10] Creating build directories...
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"
if not exist "%PUBLISH_DIR%" mkdir "%PUBLISH_DIR%"
echo ✓ Build directories created
echo.

REM Restore dependencies
echo [5/10] Restoring NuGet packages...
dotnet restore "%SOLUTION_FILE%" --verbosity minimal
if errorlevel 1 (
    echo ERROR: Failed to restore NuGet packages
    goto error_exit
)
echo ✓ NuGet packages restored
echo.

REM Build solution
echo [6/10] Building solution...
set BUILD_VERBOSITY=minimal
if "%VERBOSE%"=="true" set BUILD_VERBOSITY=normal

dotnet build "%SOLUTION_FILE%" ^
    --configuration %BUILD_CONFIG% ^
    --no-restore ^
    --verbosity %BUILD_VERBOSITY% ^
    -p:Version=%BUILD_VERSION% ^
    -p:BuildNumber=%BUILD_NUMBER% ^
    -p:GitHash=%GIT_HASH% ^
    -p:BuildDate=%BUILD_DATE%

if errorlevel 1 (
    echo ERROR: Build failed
    goto error_exit
)
echo ✓ Solution build completed
echo.

REM Run tests
if "%RUN_TESTS%"=="true" (
    echo [7/10] Running tests...
    dotnet test "%TEST_PROJECT%" ^
        --configuration %BUILD_CONFIG% ^
        --no-build ^
        --verbosity normal ^
        --logger "console;verbosity=normal" ^
        --results-directory "%BUILD_DIR%\TestResults" ^
        --collect:"XPlat Code Coverage"
    
    if errorlevel 1 (
        echo ERROR: Tests failed
        goto error_exit
    )
    echo ✓ All tests passed
) else (
    echo [7/10] Skipping tests (--no-tests specified)
)
echo.

REM Publish executables
echo [8/10] Publishing executables...

if "%BUILD_TYPE%"=="all" (
    call :publish_runtime "win-x64"
    call :publish_runtime "win-x86"
) else if "%BUILD_TYPE%"=="x64" (
    call :publish_runtime "win-x64"
) else if "%BUILD_TYPE%"=="x86" (
    call :publish_runtime "win-x86"
)

echo ✓ Executable publishing completed
echo.

REM Generate build information
echo [9/10] Generating build information...
call :generate_build_info
echo ✓ Build information generated
echo.

REM Package artifacts
echo [10/10] Packaging build artifacts...
call :package_artifacts
echo ✓ Artifacts packaged
echo.

REM Build validation
echo Performing build validation...
call :validate_build
echo ✓ Build validation completed
echo.

echo ========================================
echo BUILD COMPLETED SUCCESSFULLY
echo ========================================
echo.
echo Build artifacts location: %BUILD_DIR%
echo Published executables: %PUBLISH_DIR%
echo.
echo Available packages:
if exist "%BUILD_DIR%\CursorPhobia-%BUILD_VERSION%-win-x64.zip" echo   - CursorPhobia-%BUILD_VERSION%-win-x64.zip
if exist "%BUILD_DIR%\CursorPhobia-%BUILD_VERSION%-win-x86.zip" echo   - CursorPhobia-%BUILD_VERSION%-win-x86.zip
if exist "%BUILD_DIR%\CursorPhobia-%BUILD_VERSION%-Complete.zip" echo   - CursorPhobia-%BUILD_VERSION%-Complete.zip
echo.
echo Total build time: %BUILD_DURATION%
echo.
goto end

:show_help
echo.
echo CursorPhobia Build Script - Usage:
echo.
echo BuildScript.bat [options]
echo.
echo Options:
echo   --help         Show this help message
echo   --debug        Build in Debug configuration (default: Release)
echo   --no-tests     Skip running tests
echo   --skip-clean   Skip cleaning previous builds
echo   --x64-only     Build only x64 executable
echo   --x86-only     Build only x86 executable
echo   --verbose      Use verbose output for build operations
echo.
echo Examples:
echo   BuildScript.bat                    Build everything with default settings
echo   BuildScript.bat --debug --no-tests Build in Debug mode without tests
echo   BuildScript.bat --x64-only         Build only 64-bit executable
echo.
goto end

:generate_version
REM Generate version information from git or defaults
set BUILD_DATE=%date:~10,4%-%date:~4,2%-%date:~7,2%
set BUILD_NUMBER=%random%

REM Try to get git information
git rev-parse --short HEAD >nul 2>&1
if not errorlevel 1 (
    for /f %%i in ('git rev-parse --short HEAD') do set GIT_HASH=%%i
    for /f %%i in ('git rev-list --count HEAD') do set BUILD_NUMBER=%%i
) else (
    set GIT_HASH=unknown
)

REM Generate semantic version
set BUILD_VERSION=1.0.0-build%BUILD_NUMBER%-%GIT_HASH%

REM Check if we're on a release branch or tag
git describe --exact-match --tags HEAD >nul 2>&1
if not errorlevel 1 (
    for /f %%i in ('git describe --exact-match --tags HEAD') do (
        set TAG=%%i
        set BUILD_VERSION=!TAG:v=!
    )
)

goto :eof

:publish_runtime
set RUNTIME=%~1
echo   Publishing %RUNTIME% executable...

dotnet publish "%CONSOLE_PROJECT%" ^
    --configuration %BUILD_CONFIG% ^
    --runtime %RUNTIME% ^
    --self-contained true ^
    --output "%PUBLISH_DIR%\%RUNTIME%" ^
    -p:PublishSingleFile=true ^
    -p:PublishTrimmed=true ^
    -p:TrimMode=partial ^
    -p:Version=%BUILD_VERSION% ^
    -p:BuildNumber=%BUILD_NUMBER% ^
    -p:GitHash=%GIT_HASH% ^
    -p:BuildDate=%BUILD_DATE% ^
    --verbosity minimal

if errorlevel 1 (
    echo ERROR: Failed to publish %RUNTIME% executable
    goto error_exit
)

echo   ✓ %RUNTIME% executable published
goto :eof

:generate_build_info
set BUILD_INFO_FILE=%BUILD_DIR%\BUILD_INFO.txt

echo CursorPhobia Build Information > "%BUILD_INFO_FILE%"
echo ============================== >> "%BUILD_INFO_FILE%"
echo Version: %BUILD_VERSION% >> "%BUILD_INFO_FILE%"
echo Build Number: %BUILD_NUMBER% >> "%BUILD_INFO_FILE%"
echo Git Hash: %GIT_HASH% >> "%BUILD_INFO_FILE%"
echo Build Date: %BUILD_DATE% >> "%BUILD_INFO_FILE%"
echo Build Time: %time% >> "%BUILD_INFO_FILE%"
echo Build Configuration: %BUILD_CONFIG% >> "%BUILD_INFO_FILE%"
echo .NET Version: %DOTNET_VERSION% >> "%BUILD_INFO_FILE%"
echo Build Environment: Local Machine >> "%BUILD_INFO_FILE%"
echo. >> "%BUILD_INFO_FILE%"
echo Build Artifacts: >> "%BUILD_INFO_FILE%"
if exist "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" echo - CursorPhobia.exe (x64) - Single-file executable with all dependencies >> "%BUILD_INFO_FILE%"
if exist "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" echo - CursorPhobia.exe (x86) - Single-file executable with all dependencies >> "%BUILD_INFO_FILE%"
echo. >> "%BUILD_INFO_FILE%"
echo Usage: >> "%BUILD_INFO_FILE%"
echo - Run CursorPhobia.exe --tray to start in system tray mode >> "%BUILD_INFO_FILE%"
echo - Run CursorPhobia.exe --help for command-line options >> "%BUILD_INFO_FILE%"
echo - Double-click to run with console interface >> "%BUILD_INFO_FILE%"

goto :eof

:package_artifacts
REM Package x64 version
if exist "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" (
    echo   Creating x64 package...
    copy "%BUILD_DIR%\BUILD_INFO.txt" "%PUBLISH_DIR%\win-x64\" >nul
    if exist "README.md" copy "README.md" "%PUBLISH_DIR%\win-x64\" >nul
    if exist "LICENSE" copy "LICENSE" "%PUBLISH_DIR%\win-x64\" >nul
    
    powershell -Command "Compress-Archive -Path '%PUBLISH_DIR%\win-x64\*' -DestinationPath '%BUILD_DIR%\CursorPhobia-%BUILD_VERSION%-win-x64.zip' -Force"
    echo   ✓ x64 package created
)

REM Package x86 version
if exist "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" (
    echo   Creating x86 package...
    copy "%BUILD_DIR%\BUILD_INFO.txt" "%PUBLISH_DIR%\win-x86\" >nul
    if exist "README.md" copy "README.md" "%PUBLISH_DIR%\win-x86\" >nul
    if exist "LICENSE" copy "LICENSE" "%PUBLISH_DIR%\win-x86\" >nul
    
    powershell -Command "Compress-Archive -Path '%PUBLISH_DIR%\win-x86\*' -DestinationPath '%BUILD_DIR%\CursorPhobia-%BUILD_VERSION%-win-x86.zip' -Force"
    echo   ✓ x86 package created
)

REM Create combined package
if exist "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" if exist "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" (
    echo   Creating complete package...
    mkdir "%BUILD_DIR%\complete-package" >nul 2>&1
    copy "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" "%BUILD_DIR%\complete-package\CursorPhobia-x64.exe" >nul
    copy "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" "%BUILD_DIR%\complete-package\CursorPhobia-x86.exe" >nul
    copy "%BUILD_DIR%\BUILD_INFO.txt" "%BUILD_DIR%\complete-package\" >nul
    if exist "README.md" copy "README.md" "%BUILD_DIR%\complete-package\" >nul
    if exist "LICENSE" copy "LICENSE" "%BUILD_DIR%\complete-package\" >nul
    
    powershell -Command "Compress-Archive -Path '%BUILD_DIR%\complete-package\*' -DestinationPath '%BUILD_DIR%\CursorPhobia-%BUILD_VERSION%-Complete.zip' -Force"
    echo   ✓ Complete package created
    
    rmdir /s /q "%BUILD_DIR%\complete-package"
)

goto :eof

:validate_build
echo   Checking build artifacts...

REM Check that executables were created
set VALIDATION_PASSED=true

if "%BUILD_TYPE%"=="all" (
    if not exist "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" (
        echo   ERROR: x64 executable not found
        set VALIDATION_PASSED=false
    ) else (
        for %%F in ("%PUBLISH_DIR%\win-x64\CursorPhobia.exe") do set X64_SIZE=%%~zF
        set /a X64_SIZE_MB=!X64_SIZE!/1048576
        echo   ✓ x64 executable: !X64_SIZE_MB! MB
    )
    
    if not exist "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" (
        echo   ERROR: x86 executable not found
        set VALIDATION_PASSED=false
    ) else (
        for %%F in ("%PUBLISH_DIR%\win-x86\CursorPhobia.exe") do set X86_SIZE=%%~zF
        set /a X86_SIZE_MB=!X86_SIZE!/1048576
        echo   ✓ x86 executable: !X86_SIZE_MB! MB
    )
) else if "%BUILD_TYPE%"=="x64" (
    if not exist "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" (
        echo   ERROR: x64 executable not found
        set VALIDATION_PASSED=false
    ) else (
        echo   ✓ x64 executable created
    )
) else if "%BUILD_TYPE%"=="x86" (
    if not exist "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" (
        echo   ERROR: x86 executable not found
        set VALIDATION_PASSED=false
    ) else (
        echo   ✓ x86 executable created
    )
)

REM Basic smoke test
echo   Performing smoke tests...
if exist "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" (
    "%PUBLISH_DIR%\win-x64\CursorPhobia.exe" --help >nul 2>&1
    if not errorlevel 1 (
        echo   ✓ x64 executable responds to --help
    ) else (
        echo   WARNING: x64 executable --help test failed
    )
)

if exist "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" (
    "%PUBLISH_DIR%\win-x86\CursorPhobia.exe" --help >nul 2>&1
    if not errorlevel 1 (
        echo   ✓ x86 executable responds to --help
    ) else (
        echo   WARNING: x86 executable --help test failed
    )
)

if "%VALIDATION_PASSED%"=="false" (
    echo   ERROR: Build validation failed
    goto error_exit
)

goto :eof

:error_exit
echo.
echo ========================================
echo BUILD FAILED
echo ========================================
echo.
echo Check the error messages above for details.
echo.
exit /b 1

:end
REM Calculate build duration (approximate)
set BUILD_DURATION=Completed
echo Build script finished at %time%
endlocal
exit /b 0