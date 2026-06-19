@echo off
setlocal EnableDelayedExpansion
title Firing Solution - Installer
color 0A

echo.
echo  ================================================
echo   FIRING SOLUTION - Dependency Installer
echo  ================================================
echo.
echo  This will install everything needed to play the game.
echo  It may take a few minutes depending on your internet.
echo.
pause

:: ── Check for .NET SDK ────────────────────────────────────────────────────────
echo.
echo  [1/2] Checking for .NET SDK...

dotnet --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  .NET SDK not found. Installing via winget...
    echo.
    winget install --id Microsoft.DotNet.SDK.8 -e --silent
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo  ERROR: Could not install .NET SDK automatically.
        echo  Please download it manually from: https://dotnet.microsoft.com/download
        echo.
        pause
        exit /b 1
    )
    echo  .NET SDK installed successfully.
) else (
    for /f "tokens=*" %%v in ('dotnet --version 2^>nul') do set DOTNET_VER=%%v
    echo  .NET SDK found: !DOTNET_VER!
)

:: ── Check / Install Godot 4.3 .NET ────────────────────────────────────────────
echo.
echo  [2/2] Checking for Godot 4.3 .NET...

set GODOT_DIR=%USERPROFILE%\Godot\4.3-mono\Godot_v4.3-stable_mono_win64
set GODOT_EXE=%GODOT_DIR%\Godot_v4.3-stable_mono_win64.exe

if exist "%GODOT_EXE%" (
    echo  Godot 4.3 .NET already installed at:
    echo  %GODOT_DIR%
    goto :done
)

echo  Godot not found. Downloading Godot 4.3 .NET edition...
echo  (This is about 130 MB)
echo.

set GODOT_ZIP=%TEMP%\Godot_v4.3-stable_mono_win64.zip
set GODOT_URL=https://github.com/godotengine/godot/releases/download/4.3-stable/Godot_v4.3-stable_mono_win64.zip

:: Download with PowerShell
powershell -NoProfile -Command ^
    "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; " ^
    "$wc = New-Object System.Net.WebClient; " ^
    "$wc.DownloadFile('%GODOT_URL%', '%GODOT_ZIP%'); " ^
    "Write-Host 'Download complete.'"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  ERROR: Download failed. Check your internet connection and try again.
    echo.
    pause
    exit /b 1
)

echo.
echo  Extracting Godot...

if not exist "%USERPROFILE%\Godot\4.3-mono" mkdir "%USERPROFILE%\Godot\4.3-mono"

powershell -NoProfile -Command ^
    "Expand-Archive -Path '%GODOT_ZIP%' -DestinationPath '%USERPROFILE%\Godot\4.3-mono' -Force; " ^
    "Write-Host 'Extraction complete.'"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  ERROR: Extraction failed.
    echo.
    pause
    exit /b 1
)

del "%GODOT_ZIP%" >nul 2>&1

:done
echo.
echo  ================================================
echo   ALL DONE! Everything is installed.
echo.
echo   Now double-click PLAY.bat to start the game.
echo  ================================================
echo.
pause
