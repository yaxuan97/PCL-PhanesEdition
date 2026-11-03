@echo off
setlocal
title PCL Phanes Edition - Public Release Build
set "BUILD_SCRIPT=%~dp0tools\build\Build.ps1"
if not exist "%BUILD_SCRIPT%" (
    echo [ERROR] Build script not found:
    echo         "%BUILD_SCRIPT%"
    echo         Keep this .bat and tools\build\Build.ps1 together.
    pause
    exit /b 1
)
where powershell.exe >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Windows PowerShell was not found.
    pause
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%BUILD_SCRIPT%" -PublicRelease %*
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
if not "%BUILD_EXIT_CODE%"=="0" (
    echo.
    echo Public release build failed with exit code %BUILD_EXIT_CODE%.
    pause
)
exit /b %BUILD_EXIT_CODE%
