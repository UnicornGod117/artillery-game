@echo off
setlocal EnableDelayedExpansion
title Firing Solution
color 0A

set GODOT_EXE=%USERPROFILE%\Godot\4.3-mono\Godot_v4.3-stable_mono_win64\Godot_v4.3-stable_mono_win64.exe
set PROJECT_DIR=%~dp0shell\godot
set CSPROJ=%PROJECT_DIR%\FiringSolution.Shell.csproj

:: ── Pull latest code ─────────────────────────────────────────────────────────
echo.
echo  Checking for updates...
git -C "%~dp0" pull
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  WARNING: Could not pull latest updates. Playing current version.
    echo.
)

:: ── Preflight checks ─────────────────────────────────────────────────────────
if not exist "%GODOT_EXE%" (
    echo.
    echo  ERROR: Godot not found. Please run INSTALL.bat first.
    echo.
    pause
    exit /b 1
)

dotnet --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  ERROR: .NET SDK not found. Please run INSTALL.bat first.
    echo.
    pause
    exit /b 1
)

:: ── Build ─────────────────────────────────────────────────────────────────────
echo.
echo  Building game...
dotnet build "%CSPROJ%" -c Debug --nologo -v quiet
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  ERROR: Build failed. See errors above.
    echo.
    pause
    exit /b 1
)

:: ── Launch ───────────────────────────────────────────────────────────────────
echo  Launching Firing Solution...
"%GODOT_EXE%" --path "%PROJECT_DIR%"
