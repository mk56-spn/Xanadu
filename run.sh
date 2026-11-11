#!/usr/bin/env bash
# Builds and launches one of Xanadu's exes. With no target it asks which in
# this terminal; extra arguments go to the exe.
#
#   ./run.sh                      -> a menu
#   ./run.sh game [args]          -> the game (--scene=songselect, --play, --demo, --shot=<png> …)
#   ./run.sh editor [args]        -> the game, straight into the level editor
#   ./run.sh labs [args]          -> shader playground, editor and experiments (--tab=<n>, --shader=<file> …)
#   ./run.sh animator [args]      -> the animator (--clip=name@seconds …)
#   ./run.sh showcase [args]      -> the UI showcase
#   ./run.sh visual-tests [args]  -> the visual test browser (--visual-test=<slug> --visual-test-exit …)
#   ./run.sh atlas [args]         -> a chart of what types touch what (--focus=<type>, --compare=A,B,
#                                    --sort=used, --dump=<type>); builds the projects it reads first
#   ./run.sh autopsy [args]       -> frame autopsy of each exe's last launch
#   ./run.sh autopsy game|editor|labs|animator [args]
#                                 -> runs that target with the CPU sampler on, then opens
#                                    autopsy on METHODS once it closes
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
targets=(game editor labs animator showcase visual-tests atlas autopsy)

# Started without a terminal (a double click in the file manager): the menu would
# have nowhere to ask, so open one and run there.
if [[ ! -t 0 && -z "${XANADU_RUN_WINDOW:-}" ]]; then
    for terminal in konsole kgx gnome-terminal xterm; do
        command -v "$terminal" >/dev/null || continue
        case "$terminal" in
            gnome-terminal | kgx) exec "$terminal" -- env XANADU_RUN_WINDOW=1 "$0" "$@" ;;
            *) exec "$terminal" -e env XANADU_RUN_WINDOW=1 "$0" "$@" ;;
        esac
    done
fi

# In a window of its own, a failed build or launch stays readable until Enter.
if [[ -n "${XANADU_RUN_WINDOW:-}" ]]; then
    trap 'status=$?; if [[ $status -ne 0 ]]; then echo; read -rp "Failed ($status). Press Enter to close. "; fi' EXIT
fi

run() {
    local target="$1"
    shift
    case "$target" in
        game) dotnet run --project "$root/Game/Host" -- "$@" ;;
        editor) dotnet run --project "$root/Game/Host" -- --editor "$@" ;;
        labs) dotnet run --project "$root/Tools/Labs" -- "$@" ;;
        animator) dotnet run --project "$root/Tools/Animator" -- "$@" ;;
        showcase) dotnet run --project "$root/UI/Showcase" -- "$@" ;;
        visual-tests) dotnet run --project "$root/Tests/VisualTests" -- "$@" ;;
        atlas)
            # The chart is read from the builds; a project that fails to build shows its last good one.
            for project in Game/Xanadu.csproj Tools/Labs Tools/Animator Tools/Autopsy UI/Showcase; do
                dotnet build "$root/$project" -v q -nologo >/dev/null 2>&1 || echo "atlas: $project didn't build; showing its last build" >&2
            done
            dotnet run --project "$root/Tools/Atlas" -- "$@"
            ;;
        autopsy)
            if [[ $# -gt 0 && " game editor labs animator " == *" $1 "* ]]; then
                local profiled="$1"
                shift
                # The exe's own exit code doesn't stop the autopsy: a crash is worth looking at too.
                XANADU_PROFILE=1 run "$profiled" "$@" || true
                dotnet run --project "$root/Tools/Autopsy" -- --methods
            else
                dotnet run --project "$root/Tools/Autopsy" -- "$@"
            fi
            ;;
        *)
            echo "Unknown target '$target' (choose from: ${targets[*]})" >&2
            return 1
            ;;
    esac
}

target="${1:-}"
[[ $# -gt 0 ]] && shift

if [[ -z "$target" ]]; then
    echo "Xanadu: which one? (type its number, then Enter)"
    PS3="> "
    select target in "${targets[@]}"; do
        [[ -n "$target" ]] && break
    done
    [[ -n "$target" ]] || exit 0
fi

run "$target" "$@"
