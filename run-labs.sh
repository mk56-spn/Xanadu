#!/usr/bin/env bash
# Builds and launches Xanadu Labs (the shader playground and experiments).
# Extra arguments go to it, e.g. --shader=<file>.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec dotnet run --project "$root/Tools/Labs" -- "$@"
