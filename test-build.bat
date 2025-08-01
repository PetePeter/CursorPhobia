@echo off
echo Testing .NET build...
dotnet --version
echo.
echo Building project...
dotnet build src\Console\CursorPhobia.Console.csproj --configuration Release
pause