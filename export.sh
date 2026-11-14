#!/usr/bin/env bash
# Exports the game as a self-contained build for another machine: no .NET or repo needed there.
#
#   ./export.sh                       -> Windows x64 into ~/xanadu-export/win-x64
#   ./export.sh linux-x64             -> Linux x64 into ~/xanadu-export/linux-x64
#   ./export.sh win-x64 <folder>      -> into <folder>
#
# The folder holds the exe, Assets/ and an xanadu-game marker the game finds its assets by.
# Testing the Windows build on Linux: add Xanadu.Host.exe to Steam as a non-Steam game and
# force a Proton version under Properties → Compatibility.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
rid="${1:-win-x64}"
out="${2:-$HOME/xanadu-export/$rid}"

case "$rid" in
    win-x64 | linux-x64) ;;
    *) echo "Unknown platform '$rid' (choose from: win-x64, linux-x64)" >&2; exit 1 ;;
esac

"$root/External/fetch-moonlibs.sh" >/dev/null

rm -rf "$out"
mkdir -p "$out"
dotnet publish "$root/Game/Host" -c Release -r "$rid" --self-contained true -o "$out" -v q -nologo
cp -r "$root/Assets" "$out/Assets"
: >"$out/xanadu-game"
echo "Exported $rid to $out"
