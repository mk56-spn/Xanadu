#!/usr/bin/env bash
# Builds and launches the animator on the MoonWorks host. Extra arguments go to
# the animator (--resolution=WxH, --shot=png, --clip=name@seconds, ...).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec dotnet run --project "$root/Tools/Animator" -- "$@"
