@echo off
setlocal

set Configuration=%1
if "%Configuration%"=="" set Configuration=Release

set RepoRoot=%~dp0..
set ProjectPath=%RepoRoot%\DXMainClient\DXMainClient.csproj
set Output=%RepoRoot%\Compiled\Ares\Resources\Binaries\Windows

dotnet publish %ProjectPath% --configuration=%Configuration% -property:GAME=Ares -property:ENGINE=WindowsDX --framework=net7.0-windows --output=%Output%

if %ERRORLEVEL% neq 0 (
    echo Build failed for Ares WindowsDX net8.0-windows %Configuration%
    pause
    exit /b 1
)

pause
endlocal
