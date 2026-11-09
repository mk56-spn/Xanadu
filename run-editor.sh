#!/usr/bin/env bash
# Builds and launches the game straight into the level editor. Extra
# arguments go to the game, e.g. --shot=<file>.
set -euo pipefail

exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)/run-game.sh" --editor "$@"
