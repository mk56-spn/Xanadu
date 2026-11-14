# Builds and launches one of Xanadu's exes on Windows: the twin of run.sh.
# With no target it asks which; extra arguments go to the exe.
#
#   .\run.ps1                      -> a menu
#   .\run.ps1 game [args]          -> the game (--scene=songselect, --play, --demo, --shot=<png> …)
#   .\run.ps1 editor [args]        -> the game, straight into the level editor
#   .\run.ps1 labs [args]          -> shader playground, editor and experiments
#   .\run.ps1 animator [args]      -> the animator
#   .\run.ps1 shaders [args]       -> the shader tool
#   .\run.ps1 showcase [args]      -> the UI showcase
#   .\run.ps1 visual-tests [args]  -> the visual test browser
#   .\run.ps1 devtools [args]      -> Atlas, Autopsy, Moves and Arch (see run.sh for the tabs)
#   .\run.ps1 devtools autopsy game|editor|labs|animator|shaders [args]
#                                  -> runs that target with the CPU sampler on, then opens AUTOPSY
#
# If scripts are blocked: powershell -ExecutionPolicy Bypass -File run.ps1 [target] [args]
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$targets = @('game', 'editor', 'labs', 'animator', 'shaders', 'showcase', 'visual-tests', 'devtools')
$profiled = @('game', 'editor', 'labs', 'animator', 'shaders')

function Invoke-Project([string]$project, [string[]]$exeArgs) {
    & dotnet run --project (Join-Path $root $project) -- @exeArgs
    if ($LASTEXITCODE -ne 0) { throw "$project exited with $LASTEXITCODE" }
}

function Invoke-Target([string]$target, [string[]]$rest) {
    switch ($target) {
        'game' { Invoke-Project 'Game/Host' $rest }
        'editor' { Invoke-Project 'Game/Host' (@('--editor') + $rest) }
        'labs' { Invoke-Project 'Tools/Labs' $rest }
        'animator' { Invoke-Project 'Tools/Animator' $rest }
        'shaders' { Invoke-Project 'Tools/Shaders' $rest }
        'showcase' { Invoke-Project 'UI/Showcase' $rest }
        'visual-tests' { Invoke-Project 'Tests/VisualTests' $rest }
        'devtools' {
            if ($rest.Count -gt 1 -and $rest[0] -eq 'autopsy' -and $profiled -contains $rest[1]) {
                $env:XANADU_PROFILE = '1'
                # The exe's own exit code doesn't stop the autopsy: a crash is worth looking at too.
                try { Invoke-Target $rest[1] @($rest | Select-Object -Skip 2) } catch { Write-Warning $_ }
                finally { Remove-Item Env:XANADU_PROFILE -ErrorAction SilentlyContinue }
                Invoke-Project 'Tools/DevTools' @('autopsy', '--methods')
                return
            }
            # Atlas reads its chart from the builds; a project that fails to build shows its last good one.
            foreach ($project in @('Game/Xanadu.csproj', 'Tools/Labs', 'Tools/Animator', 'Tools/Shaders', 'Tools/DevTools', 'UI/Showcase')) {
                Write-Host "devtools: building $project for Atlas..."
                & dotnet build (Join-Path $root $project) -v q -nologo | Out-Null
                if ($LASTEXITCODE -ne 0) { Write-Warning "devtools: $project didn't build; Atlas shows its last build" }
            }
            Invoke-Project 'Tools/DevTools' $rest
        }
        default { throw "Unknown target '$target' (choose from: $($targets -join ', '))" }
    }
}

$target = if ($args.Count -gt 0) { [string]$args[0] } else { '' }
$rest = [string[]]@($args | Select-Object -Skip 1)

if ($target -eq '') {
    Write-Host 'Xanadu: which one?'
    for ($i = 0; $i -lt $targets.Count; $i++) { Write-Host ("  {0}  {1}" -f ($i + 1), $targets[$i]) }
    $pick = Read-Host 'Number (Enter quits)'
    if ($pick -notmatch '^\d+$' -or [int]$pick -lt 1 -or [int]$pick -gt $targets.Count) { exit 0 }
    $target = $targets[[int]$pick - 1]
}

Invoke-Target $target $rest
