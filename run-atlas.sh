#!/usr/bin/env bash
# Builds the projects, then opens Atlas: a chart of what types touch what
# (systems and the components they query, interfaces, bases, uses).
#   ./run-atlas.sh                          -> the tool
#   ./run-atlas.sh --focus=TextEditor       -> opens on a type
#   ./run-atlas.sh --compare=TextField,TextEditor
#   ./run-atlas.sh --sort=used              -> list sorted by name, used, uses, type or project
#   ./run-atlas.sh --dump=TextEditor        -> a type's links as text, no window
# The chart is read from the builds; a project that fails to build shows its last good one.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
for project in Xanadu.csproj Tools/Labs Tools/Animator Tools/Autopsy UI/Showcase; do
    dotnet build "$root/$project" -v q -nologo >/dev/null 2>&1 || echo "atlas: $project didn't build; showing its last build" >&2
done
exec dotnet run --project "$root/Tools/Atlas" -- "$@"
