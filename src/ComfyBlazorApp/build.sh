#!/usr/bin/env bash
set -e

echo "============================================================"
echo " ComfyBlazorApp - Build Script (Linux / macOS)"
echo "============================================================"
echo

# ── 1. Install libman CLI tool (idempotent) ──────────────────────
echo "[1/4] Installing LibMan CLI..."
dotnet tool install -g Microsoft.Web.LibraryManager.Cli 2>/dev/null || \
dotnet tool update  -g Microsoft.Web.LibraryManager.Cli 2>/dev/null || true
echo "      Done."
echo

# Ensure ~/.dotnet/tools is on PATH for this session
export PATH="$PATH:$HOME/.dotnet/tools"

# ── 2. Restore client-side libraries ─────────────────────────────
echo "[2/4] Restoring client-side libraries (libman restore)..."
libman restore
echo "      Done."
echo

# ── 3. Restore .NET packages ──────────────────────────────────────
echo "[3/4] Restoring .NET packages..."
dotnet restore
echo "      Done."
echo

# ── 4. Build the application ──────────────────────────────────────
echo "[4/4] Building ComfyBlazorApp..."
dotnet build --no-restore -c Release
echo
echo "============================================================"
echo " Build completed successfully."
echo
echo " To run the app:"
echo "   dotnet run"
echo
echo " Make sure ComfyUI is running at http://127.0.0.1:8188"
echo "============================================================"
