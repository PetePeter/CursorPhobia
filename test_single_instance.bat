@echo off
echo Testing Phase 1 WI#8: Single Instance Manager
echo ==========================================

cd /d "X:\coding\CursorPhobia"

echo.
echo Starting first instance (should succeed)...
start "First Instance" dotnet run --project src/Console/CursorPhobia.Console.csproj -- --automated

echo Waiting 3 seconds for first instance to initialize...
timeout /t 3 >nul

echo.
echo Starting second instance (should detect first and exit)...
dotnet run --project src/Console/CursorPhobia.Console.csproj -- --automated

echo.
echo Testing complete. Check the output above for single instance behavior.
pause