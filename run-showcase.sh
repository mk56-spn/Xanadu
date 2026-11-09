#!/usr/bin/env bash
# Builds and launches the UI showcase on the MoonWorks host. Extra arguments go
# to the showcase (--resolution=WxH, --shot=png).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec dotnet run --project "$root/UI/Showcase" -- "$@"
