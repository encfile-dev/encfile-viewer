@echo off
setlocal enabledelayedexpansion

echo ============================================================
echo  ComfyBlazorApp - Build Script (Windows)
echo ============================================================
echo.

:: ── 1. Install libman CLI tool (idempotent) ──────────────────────
echo [1/4] Installing LibMan CLI...
dotnet tool install -g Microsoft.Web.LibraryManager.Cli 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo       LibMan CLI already installed or update available.
    dotnet tool update -g Microsoft.Web.LibraryManager.Cli 2>nul
)
echo       Done.
echo.

:: ── 2. Restore client-side libraries ─────────────────────────────
echo [2/4] Restoring client-side libraries (libman restore)...
libman restore
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] libman restore failed. Make sure libman CLI is in your PATH.
    echo         Try running: refreshenv  (if using Chocolatey)
    echo         Or open a new terminal after tool install.
    exit /b 1
)
echo       Done.
echo.

:: ── 3. Restore .NET packages ──────────────────────────────────────
echo [3/4] Restoring .NET packages...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] dotnet restore failed.
    exit /b 1
)
echo       Done.
echo.

:: ── 4. Build the application ──────────────────────────────────────
echo [4/4] Building ComfyBlazorApp...
dotnet build --no-restore -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed.
    exit /b 1
)
echo.
echo ============================================================
echo  Build completed successfully.
echo.
echo  To run the app:
echo    dotnet run
echo.
echo  Make sure ComfyUI is running at http://127.0.0.1:8188
echo ============================================================
endlocal
