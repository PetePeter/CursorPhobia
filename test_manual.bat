@echo off
echo Testing single instance manually
cd /d "X:\coding\CursorPhobia"
dotnet run --project src/Console/CursorPhobia.Console.csproj
pause