# Hand-off: render-architecture.md: all steps landed

Updated 2026-11-06. `docs/render-architecture.md` has steps **0–12 ticked**,
plus review follow-ups noted under steps 4, 8, 10 and 11. Steps 0–12 are
committed (`592f945` … `d5ab569`); the follow-ups are not yet.

## Where things stand

- Buffered groups fade as one image: the group's alpha is on the buffer
  (D14 changed 2026-11-06). The lock is enforced in `EditorHistory.Record`
  for every edit, with `EditorBufferGuard` as the up-front check.
- Shader keys: the group panel's key lane (pins, ease bands, drag to retime,
  typed time and value, EASE, delete), SHD as a named list in both panels,
  unkeyed settings at the shader's declared default.
- Layers panel: MOVE ▲ / ▼, DELETE (empty only), pass tags on rows.
- Screen-read regions can be grouped and start sized to the view.

## Not done

- The layers panel's pass track has no timeline of its own (keys are edited
  with the row's − / + only).
- Optional from step 12: a whole character as a buffered group.

## Verification

Build: `dotnet build Xanadu.csproj`, `Tests/Xanadu.Tests.csproj`,
`Tests/VisualTests/Xanadu.VisualTests.csproj`. Run test namespaces one at a
time (a single full run aborts in the test host). Visual tests:
`editor-groups`, `editor-layers`, `editor-screen-read`, `editor-blend-choice`,
`play-buffered-group`.

## Working notes

- Godot binary:
  `/home/dhs/Documents/Godot_v4.8-dev7_mono_linux_x86_64/Godot_v4.8-dev7_mono_linux.x86_64`
- Visual test + screenshot:
  `"$GODOT" --path Tests/VisualTests --fullscreen res://VisualTests.tscn -- --visual-test=<slug> --visual-test-shot=/var/home/dhs/xanadu/screenshots/<slug>.png`
  Without `--visual-test-shot` the harness doesn't exit. A run killed
  mid-edit leaves `user://recovery/untitled.recovery.xmf` in the
  "Xanadu Visual Tests" user data, and the next editor test opens on the
  recovery dialog; delete the file.
