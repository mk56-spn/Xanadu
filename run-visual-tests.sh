#!/usr/bin/env bash
# Builds and launches the visual test browser on the MoonWorks host. Extra
# arguments go to it, e.g. --visual-test=settings --visual-test-exit.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec dotnet run --project "$root/Tests/VisualTests" -- "$@"
