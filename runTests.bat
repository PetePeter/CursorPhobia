@echo off
echo Running CursorPhobia Tests
echo =========================

echo.
echo Building solution...
dotnet build CursorPhobia.sln >nul 2>&1

if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED!
    dotnet build CursorPhobia.sln
    pause
    exit /b 1
)

echo.
echo Running unit tests...
dotnet test tests/CursorPhobia.Tests.csproj --logger "console;verbosity=minimal" | findstr /V "Passed" | findstr /V "Starting test execution" | findstr /V "Total tests:" | findstr /V "Test Run Successful" | findstr /V "^$"

if %ERRORLEVEL% neq 0 (
    echo UNIT TESTS FAILED!
    dotnet test tests/CursorPhobia.Tests.csproj --logger "console;verbosity=normal"
    pause
    exit /b 1
)

echo.
echo Running console test application...
dotnet run --project src/Console/CursorPhobia.Console.csproj >nul 2>&1

if %ERRORLEVEL% neq 0 (
    echo CONSOLE TEST FAILED!
    dotnet run --project src/Console/CursorPhobia.Console.csproj
    pause
    exit /b 1
)

echo All tests passed!
pause