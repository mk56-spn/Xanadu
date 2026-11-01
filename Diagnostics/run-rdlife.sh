#!/usr/bin/env bash
# Builds the project and launches the Game of Life scene with the newest mono
# Godot in ~/Documents. Extra arguments go to Godot, e.g. -- --shot=<file>.
set -euo pipefail

project="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

godot="$(find "$HOME/Documents" -maxdepth 2 -type f -executable \
    -name 'Godot_*_mono_linux*.x86_64' | sort -V | tail -n 1)"
if [[ -z "$godot" ]]; then
    echo "No mono Godot found under $HOME/Documents" >&2
    exit 1
fi

dotnet build "$project/Xanadu.csproj" --nologo --verbosity quiet
exec "$godot" --path "$project" res://Diagnostics/RdLife.tscn "$@"
