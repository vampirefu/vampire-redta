@echo off

set Configuration=%1
if "%Configuration%"=="" set Configuration=Release

set RepoRoot=%~dp0..
set ProjectPath=%RepoRoot%\DXMainClient\DXMainClient.csproj
set CompiledRoot=%RepoRoot%\Compiled

call :Build-Project %Configuration% Ares WindowsDX net7.0-windows
if %errorlevel% neq 0 (
    echo.
    echo [FAILED] Build failed!
    pause
    goto :eof
)

echo.
echo [SUCCESS] Build completed successfully!
echo Output: %CompiledRoot%\Ares\Resources\Binaries\Windows
pause
goto :eof

:Build-Project
setlocal
set BuildConfig=%~1
set Game=%~2
set Engine=%~3
set Framework=%~4

if "%Engine%"=="UniversalGL" set EngineDir=UniversalGL
if "%Engine%"=="WindowsDX" set EngineDir=Windows
if "%Engine%"=="WindowsGL" set EngineDir=OpenGL

set Output=%CompiledRoot%\%Game%\Resources\Binaries\%EngineDir%

dotnet publish %ProjectPath% --configuration=%BuildConfig% -property:GAME=%Game% -property:ENGINE=%Engine% --framework=%Framework% --output=%Output%
if %errorlevel% neq 0 (
    echo Build failed for %Game% %Engine% %Framework% %BuildConfig%
    exit /b 1
)
endlocal
exit /b 0
