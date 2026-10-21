# Scene tests

`Tests/VisualTests/VisualTests.tscn` is a small osu!framework-inspired test scene runner. Each
test is a sequence of steps executed against the live ECS store. It does not
take or compare screenshots.

## The step model

Tests derive from `SceneTest` and build steps in `SetUpSteps`:

```csharp
AddStep("open settings", () => Context.Scenes.Start(SceneKind.Settings));
AddUntilStep("wait for settings ECS", () => ...);
AddAssert("keybinds are above background", () => ...);
AddWaitStep("settle", 2);
AddRepeatStep("blink", () => ..., 3);
```

`SceneTestRunner` executes one step at a time, with a short settle delay
between steps. A failed assertion or timed-out `AddUntilStep` stops the test
and reports the step description.

Readiness is expressed with `AddUntilStep` rather than a fixed frame count.

## Run

```bash
GODOT=/path/to/Godot_v4.8-dev7_mono_linux.x86_64

# Interactive browser
"$GODOT" --path . res://Tests/VisualTests/VisualTests.tscn

# One test, exit with 0/1
"$GODOT" --path . res://Tests/VisualTests/VisualTests.tscn -- \
  --visual-test=settings --visual-test-exit

# All tests
"$GODOT" --path . res://Tests/VisualTests/VisualTests.tscn -- \
  --visual-test=all --visual-test-exit
```

Interactive controls:

- `Left` / `Right` — switch test
- `R` — rerun current test
- `Esc` — quit

The left sidebar lists every test, marks the current one with `>`, and shows
every step as `pending`, `running`, `passed`, or `failed`.

## Current tests

- `main` — menu text, animated PLAY button, initialized menu layer
- `song-select` — song select text and initialized menu layer
- `settings` — keybind steps:
  - seven keybind rows exist
  - content layer is above background
  - every keybind is on the content layer
  - every keybind is parented to the content layer
  - every keybind is animated
  - every keybind rectangle is non-empty
- `play` — synthetic level, score/combo HUD, animated PAUSE button, initialized
  play HUD layer

## Adding a test

Add a `SceneTest` subclass to `SceneTestCatalog.All`. Build its steps in
`SetUpSteps`; use `Context.Entities` and `Context.Scenes` for live assertions.
`SceneTestRunnerTests` covers step ordering, assertion failure, and timeout.
