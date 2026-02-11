@echo off
dotnet-format Diffy.sln
cls && dotnet test tests\Diffy.Tests.Unit\Diffy.Tests.Unit.csproj