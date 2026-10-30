# Visual tests

`VisualTests.tscn` is an osu!framework-inspired scene-test runner for the ECS
screens. Each test is a sequence of steps run against a live ECS store, so it can
assert on entities, input and layout the way the running game does.

It does **not** compare screenshots. It can save the window on demand so a human
or an agent can look at it — see [Capturing](#capturing).

## How it is built

- The sidebar is ECS UI, like the rest of the project: a title, `RUN ALL`, one
  row per test and the current test's steps. No Godot widget is created.
- The pane beside it is a `Node2D` host offset by the sidebar width, so the
  scene renders in the main viewport next to the sidebar.
- Everything is authored at 1920x1080 and `project.godot` scales the whole
  surface as 16:9 (`canvas_items` + `keep`), so the harness never reads the
  window size.

## Run

```bash
GODOT=/path/to/Godot_v4.8-dev7_mono_linux.x86_64

# Interactive browser
"$GODOT" --path Tests/VisualTests res://VisualTests.tscn

# One test, exit with 0/1
"$GODOT" --path Tests/VisualTests res://VisualTests.tscn -- \
  --visual-test=settings --visual-test-exit

# All tests
"$GODOT" --path Tests/VisualTests res://VisualTests.tscn -- \
  --visual-test=all --visual-test-exit

# Save the window to a PNG once it settles, then exit
"$GODOT" --path Tests/VisualTests --fullscreen res://VisualTests.tscn -- \
  --visual-test=settings --visual-test-shot=/tmp/harness.png
```

`--visual-test-exit` is the CI-only exit switch: without it, finishing a run
leaves the window open. `--visual-test=all` keeps going past failures.

## Controls

- `RUN ALL` — run every test and mark each row
- click a row once — select it
- click the selected row again — run just that test
- `Left` / `Right` — change selection
- `R` / `Enter` — run the selected test
- `Esc` — quit

## Capturing

`--visual-test-shot=<path>` reads the harness's own viewport texture, so it needs
neither the compositor nor the window to be visible or focused. It does need a
real render pass: do not combine it with `--headless`, which cannot render.

Each capture happens once, after 60 frames, then the process exits 0 (or 1 if
the PNG could not be written). On Wayland this is the only dependable way to
capture the harness — desktop tools see the session, not the game's surface.
Write the file under the repo; `user://` is read-only in the sandbox.

## The step model

Tests derive from `SceneTest` and build steps in `SetUpSteps`:

```csharp
AddStep("open settings", () => Context.Scenes.Start(SceneKind.Settings));
AddUntilStep("wait for settings ECS", () => ...);
AddAssert("keybinds are above background", () => ...);
AddWaitStep("settle", 2);
AddRepeatStep("blink", () => ..., 3);
```

`SceneTestRunner` executes one step at a time, with a short settle delay between
steps. A failed assertion or timed-out `AddUntilStep` stops the test and reports
the step description. Readiness is expressed with `AddUntilStep` rather than a
fixed frame count.

## Tests

| Slug | Test |
| --- | --- |
| `profiles` | Profiles |
| `main` | Main menu |
| `song-select` | Song select |
| `editor-waveform` | Editor waveform |
| `editor-metadata` | Editor metadata |
| `settings` | Settings keybinds |
| `offset-wizard` | Offset wizard |
| `play` | Play HUD and field |
| `rainbow-note-shader` | Note with the common rainbow shader |
| `movement-judgement` | Movement judgement |
| `results` | Results |
| `restart` | Retry |
| `editor-dropdowns` | Editor dropdowns |
| `editor-dash-wheel` | Editor dash wheel |
| `character-notes` | Pair, dash, momentum and switch note bodies |
| `splitline-rail` | Splitline rail and taps |
| `note-key-checking` | A press only hits its own note key |
| `editor-groups` | Editor groups |
| `editor-focus` | Editor focus and tooltips |
| `play-groups` | Play groups |
| `leave-editor` | Leave editor |

The runner prints this list when given an unknown slug.

## NUnit integration

The scene tests cannot be hosted inside the NUnit process: they need Godot's main
loop and native engine initialization, and `RenderingServer`, input, audio and
the scene tree do not exist in the NUnit host.

NUnit can act as an orchestrator instead, launching Godot as a child process and
parsing the runner output. That is implemented as one NUnit test per scene in
`NUnitIntegrationTests.cs`, so Rider shows a run arrow beside each visual scene.
The tests skip when `GODOT_BIN` is not set:

```bash
GODOT_BIN=/path/to/Godot_v4.8-dev7_mono_linux.x86_64 \
  dotnet test Xanadu.sln --filter VisualSceneTests
```

The split:

- NUnit: engine-independent unit and integration tests.
- Godot scene runner: live ECS, input, timing and rendering behaviour.
- NUnit opt-in adapter: process-level smoke/regression run for CI.

## Adding a test

Add a `SceneTest` subclass to `SceneTestCatalog.All` and build its steps in
`SetUpSteps`. Use `Context.Entities` and `Context.Scenes` for live assertions.
`SceneTestRunnerTests` covers step ordering, assertion failure and timeout.
