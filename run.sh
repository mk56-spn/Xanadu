#!/usr/bin/env bash
# Asks where to go, then runs that launcher. Skip the menu with ./run.sh editor.
# Extra arguments go to the chosen launcher.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
targets=(game editor animator showcase labs visual-tests)

target="${1:-}"
[[ $# -gt 0 ]] && shift

if [[ -z "$target" ]]; then
    PS3="Go to: "
    select target in "${targets[@]}"; do
        [[ -n "$target" ]] && break
    done
    [[ -n "$target" ]] || exit 0
fi

if [[ ! " ${targets[*]} " == *" $target "* ]]; then
    echo "Unknown target '$target' (choose from: ${targets[*]})" >&2
    exit 1
fi

exec "$root/run-$target.sh" "$@"
