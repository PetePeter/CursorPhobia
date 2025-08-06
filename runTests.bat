@echo off
echo CursorPhobia - Build and Test
echo =============================

echo [1/3] Building solution...
dotnet build CursorPhobia.sln --configuration Release --verbosity minimal

if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED!
    dotnet build CursorPhobia.sln --configuration Release --verbosity normal
    pause
    exit /b 1
)

echo [2/3] Running unit tests...
dotnet test tests/CursorPhobia.Tests.csproj --configuration Release --no-build --logger "console;verbosity=normal"

if %ERRORLEVEL% neq 0 (
    echo TESTS FAILED!
    pause
    exit /b 1
)

echo [3/3] Running console integration test...
dotnet run --project src/Console/CursorPhobia.Console.csproj --configuration Release --no-build -- --automated

if %ERRORLEVEL% neq 0 (
    echo INTEGRATION TEST FAILED!
    pause
    exit /b 1
)

echo.
echo ✓ All tests passed!
echo ✓ Build and tests completed successfully!
pause