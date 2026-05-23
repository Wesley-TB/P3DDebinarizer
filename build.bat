@echo off
setlocal
set "ROOT=%~dp0"
set "RELEASE=%ROOT%release"

cd /d "%ROOT%P3DDebin"

rem Encerra instancias abertas para liberar o .exe antes do build
taskkill /IM P3DDebin.exe /F >nul 2>&1

if not exist "%RELEASE%" mkdir "%RELEASE%"

dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:IncludeAllContentForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=embedded ^
    -o "%RELEASE%"

pause
endlocal
