#!/usr/bin/env bash
# Opens Autopsy of a Frame on the last launch of each exe.
#   ./run-autopsy.sh                  -> the tool
#   ./run-autopsy.sh game|editor|labs|animator [args]
#                                     -> runs that launcher with the CPU sampler on
#                                        (XANADU_PROFILE=1), then opens the tool on
#                                        METHODS once you close it
# Other arguments go to the tool (--methods, --shot=<png>).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
profiled=(game editor labs animator)

if [[ $# -gt 0 && " ${profiled[*]} " == *" $1 "* ]]; then
    target="$1"
    shift
    # The game's own exit code doesn't stop the autopsy: a crash is worth looking at too.
    XANADU_PROFILE=1 "$root/run-$target.sh" "$@" || true
    exec dotnet run --project "$root/Tools/Autopsy" -- --methods
fi

exec dotnet run --project "$root/Tools/Autopsy" -- "$@"
