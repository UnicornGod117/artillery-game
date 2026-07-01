#!/usr/bin/env bash
# Firing Solution — Linux / macOS launcher (the cross-platform counterpart to PLAY.bat).
# Builds the latest code and launches the Godot 4.3 .NET shell. After a `git pull` it always
# rebuilds from scratch (--no-incremental), so you never run a stale cached version.
#
# Requires: the .NET 8 SDK and the Godot 4.3 .NET (mono) editor.
#   • Point GODOT at your editor binary, or put it on your PATH as `godot`/`godot4`.
#     e.g.  GODOT=/opt/Godot_v4.3-stable_mono/Godot ./play.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/shell/godot"
CSPROJ="$PROJECT_DIR/FiringSolution.Shell.csproj"

# ── Locate Godot ───────────────────────────────────────────────────────────────
if [[ -n "${GODOT:-}" ]]; then
    GODOT_BIN="$GODOT"
else
    GODOT_BIN=""
    for candidate in godot godot4 Godot; do
        if command -v "$candidate" >/dev/null 2>&1; then GODOT_BIN="$candidate"; break; fi
    done
fi

if [[ -z "$GODOT_BIN" ]] || { ! command -v "$GODOT_BIN" >/dev/null 2>&1 && [[ ! -x "$GODOT_BIN" ]]; }; then
    echo "ERROR: Godot 4.3 .NET editor not found." >&2
    echo "       Set GODOT to its path, e.g.  GODOT=/path/to/Godot ./play.sh" >&2
    exit 1
fi

# ── Preflight: .NET SDK ─────────────────────────────────────────────────────────
if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: .NET 8 SDK not found. Install it from https://dotnet.microsoft.com/download" >&2
    exit 1
fi

# ── Build ────────────────────────────────────────────────────────────────────────
echo "Building game..."
dotnet build "$CSPROJ" -c Debug --nologo -v quiet --no-incremental

# ── Launch ─────────────────────────────────────────────────────────────────────────
echo "Launching Firing Solution..."
exec "$GODOT_BIN" --path "$PROJECT_DIR"
