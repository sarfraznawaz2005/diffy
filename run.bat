@echo off
dotnet clean src/Diffy.App/Diffy.App.csproj && dotnet build src\Diffy.App\Diffy.App.csproj && dotnet run --project src\Diffy.App\Diffy.App.csproj