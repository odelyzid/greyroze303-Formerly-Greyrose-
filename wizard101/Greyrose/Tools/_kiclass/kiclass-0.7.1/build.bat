@echo off
setlocal enabledelayedexpansion

REM Build script for kiclass wheels on Windows
REM Supports both release and development builds

set VENV_DIR=venv
set DIST_DIR=dist

if "%1"=="" goto :help
if "%1"=="release" goto :release
if "%1"=="dev" goto :dev
if "%1"=="install" goto :install
if "%1"=="clean" goto :clean
if "%1"=="setup" goto :setup
goto :help

:setup
echo Creating virtual environment...
if not exist %VENV_DIR% (
    python -m venv %VENV_DIR%
)
call %VENV_DIR%\Scripts\activate.bat

echo Installing maturin...
pip install maturin
echo Virtual environment setup complete.
goto :end

:release
echo Checking for Git tag...
for /f "delims=" %%i in ('git describe --tags --exact 2^>nul') do set TAG=%%i
if "!TAG!"=="" (
    echo Error: No Git tag found. Create a tag first:
    echo   git tag v0.1.2
    echo   git push origin v0.1.2
    goto :end
)

REM Extract version from tag (remove 'v' prefix if present)
set VERSION=!TAG!
if "!VERSION:~0,1!"=="v" set VERSION=!VERSION:~1!

echo Building release version !VERSION! from tag !TAG!
REM Auto-setup venv if it doesn't exist
if not exist %VENV_DIR% goto :setup
call %VENV_DIR%\Scripts\activate.bat

REM Backup files
copy Cargo.toml Cargo.toml.bak >nul
copy pyproject.toml pyproject.toml.bak >nul

REM Update version in Cargo.toml
set VERSION_PS=!VERSION!
powershell -NoProfile -Command "$content = Get-Content Cargo.toml -Raw; $version = $env:VERSION_PS; $content = $content -replace '(?m)^version = \".*\"', (\"version = `\"\" + $version + \"`\"\"); Set-Content -Path Cargo.toml -Value $content -NoNewline"
set VERSION_PS=

REM Update version in pyproject.toml
set VERSION_PS=!VERSION!
powershell -NoProfile -Command "$lines = Get-Content pyproject.toml; $version = $env:VERSION_PS; $newLines = $lines | ForEach-Object { if ($_ -match '^dynamic = ') { \"version = `\"$version`\"\" } else { $_ } }; $newLines | Set-Content pyproject.toml"
set VERSION_PS=

REM Build the wheels
maturin build --release --out %DIST_DIR% --strip --features py

REM Restore original files
move /y Cargo.toml.bak Cargo.toml >nul
move /y pyproject.toml.bak pyproject.toml >nul

echo Release wheel built successfully with version !VERSION!
goto :end

:dev
echo Building development version...

REM Get commit hash
for /f "delims=" %%i in ('git rev-parse --short HEAD') do set COMMIT=%%i

REM Get the latest tag version, or use 0.1.0 as fallback
for /f "delims=" %%i in ('git describe --tags --abbrev=0 2^>nul') do set LATEST_TAG=%%i
if "!LATEST_TAG!"=="" (
    set BASE_VERSION=0.1.0
) else (
    REM Remove 'v' prefix if present
    set BASE_VERSION=!LATEST_TAG!
    if "!BASE_VERSION:~0,1!"=="v" set BASE_VERSION=!BASE_VERSION:~1!
)

REM Create PEP 440 compliant dev version: base.dev0+commit
set DEV_VERSION=!BASE_VERSION!.dev0+!COMMIT!

echo Building development version !DEV_VERSION!
echo Commit hash: !COMMIT!

REM Auto-setup venv if it doesn't exist
if not exist %VENV_DIR% goto :setup
call %VENV_DIR%\Scripts\activate.bat

REM Backup pyproject.toml
copy pyproject.toml pyproject.toml.bak >nul

REM Update version in pyproject.toml temporarily using PowerShell
REM Change dynamic = ["version"] to version = "dev_version"
set DEV_VERSION_PS=!DEV_VERSION!
powershell -NoProfile -Command "$lines = Get-Content pyproject.toml; $version = $env:DEV_VERSION_PS; $newLines = $lines | ForEach-Object { if ($_ -match '^dynamic = ') { \"version = `\"$version`\"\" } else { $_ } }; $newLines | Set-Content pyproject.toml"
set DEV_VERSION_PS=

REM Build the wheel with the dev version
maturin build --release --out %DIST_DIR% --strip --features py

REM Restore pyproject.toml
move /y pyproject.toml.bak pyproject.toml >nul

echo Development wheel built successfully with version !DEV_VERSION!
goto :end

:install
echo Installing most recently built wheel...
if not exist %DIST_DIR% (
    echo No wheels found in dist directory. Build first.
    goto :end
)

for /f "delims=" %%i in ('dir /b /o:-d %DIST_DIR%\*.whl') do set LATEST_WHEEL=%%i
if "!LATEST_WHEEL!"=="" (
    echo No wheels found in dist directory. Build first.
    goto :end
)

echo Installing !LATEST_WHEEL!...
pip install %DIST_DIR%\!LATEST_WHEEL! --force-reinstall
goto :end

:clean
echo Cleaning dist directory...
if exist %DIST_DIR% rmdir /s /q %DIST_DIR%
if exist %VENV_DIR% rmdir /s /q %VENV_DIR%
echo Cleaned dist and virtual environment directories.
goto :end

:help
echo Usage: build.bat [command]
echo.
echo Commands:
echo   setup    - Create virtual environment and install dependencies
echo   release  - Build a release wheel (requires Git tag)
echo   dev      - Build a development wheel
echo   install  - Install most recently built wheel
echo   clean    - Clean dist and venv directories
echo.
echo Examples:
echo   build.bat setup
echo   build.bat release
echo   build.bat dev
echo   build.bat install
echo   build.bat clean

:end
endlocal





