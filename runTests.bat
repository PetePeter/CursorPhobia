@echo off
echo Running CursorPhobia Phase 1 Tests
echo ==================================

echo.
echo Building solution...
dotnet build CursorPhobia.sln

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Running unit tests...
dotnet test tests/CursorPhobia.Tests.csproj --logger "console;verbosity=normal"

if %ERRORLEVEL% neq 0 (
    echo Unit tests failed!
    pause
    exit /b 1
)

echo.
echo Running console test application...
dotnet run --project src/Console/CursorPhobia.Console.csproj

echo.
echo All tests completed!
pause