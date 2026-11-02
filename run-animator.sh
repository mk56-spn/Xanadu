#!/usr/bin/env bash
# Builds and launches the animator tool. Set GODOT to use another binary.
# Extra arguments go to Godot.
set -euo pipefail

project="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)/Tools/Animator"
godot="${GODOT:-$HOME/Documents/Godot_v4.8-dev7_mono_linux_x86_64/Godot_v4.8-dev7_mono_linux.x86_64}"

if [[ ! -x "$godot" ]]; then
    echo "Godot not found at $godot (set GODOT to override)" >&2
    exit 1
fi

dotnet build "$project/Xanadu.AnimatorTool.csproj" --nologo --verbosity quiet
exec "$godot" --path "$project" res://Animator.tscn "$@"
