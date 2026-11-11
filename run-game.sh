#!/usr/bin/env bash
# Builds and launches the game on the MoonWorks host. Extra arguments go to
# it, e.g. --shot=<file>, --scene=songselect or --editor.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec dotnet run --project "$root/Game/Host" -- "$@"
