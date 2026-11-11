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

# The menu: ↑/↓ (or k/j) move, Enter picks, a number picks at once, q or Esc quits.
choose() {
    local count=${#targets[@]} at=0 key rest i
    echo "Xanadu: which one?  ↑/↓ move · Enter runs · q quits" >&2
    tput civis >&2 2>/dev/null || true
    trap 'tput cnorm >&2 2>/dev/null || true' RETURN
    while true; do
        for ((i = 0; i < count; i++)); do
            if ((i == at)); then
                printf '\e[2K  \e[7m %d  %-14s\e[0m\n' "$((i + 1))" "${targets[i]}" >&2
            else
                printf '\e[2K   %d  %s\n' "$((i + 1))" "${targets[i]}" >&2
            fi
        done

        IFS= read -rsn1 key || return 1
        case "$key" in
            $'\e')
                rest=""
                IFS= read -rsn2 -t 0.05 rest || true
                case "$rest" in
                    '[A') at=$(((at + count - 1) % count)) ;;
                    '[B') at=$(((at + 1) % count)) ;;
                    '') return 1 ;;
                esac
                ;;
            k) at=$(((at + count - 1) % count)) ;;
            j) at=$(((at + 1) % count)) ;;
            q) return 1 ;;
            '') break ;;
            [1-9])
                if ((key <= count)); then
                    at=$((key - 1))
                    break
                fi
                ;;
        esac
        printf '\e[%dA' "$count" >&2
    done

    target="${targets[at]}"
}

if [[ -z "$target" ]]; then
    choose || exit 0
    echo "→ $target" >&2
fi

run "$target" "$@"
