# Visual tests

`VisualTests.tscn` boots ECS scenes directly, without navigating the menus,
waits for the scene and its rendering layers to finish initializing, then
asserts the live ECS state. It does not take or compare screenshots; the
checks are structural.

## Run

```bash
GODOT=/path/to/Godot_v4.8-dev7_mono_linux.x86_64

# Interactive browser
"$GODOT" --path . res://VisualTests.tscn

# One test, exit with 0/1
"$GODOT" --path . res://VisualTests.tscn -- \
  --visual-test=settings --visual-test-exit

# All tests
"$GODOT" --path . res://VisualTests.tscn -- \
  --visual-test=all --visual-test-exit
```

Interactive controls:

- `Left` / `Right` — switch test
- `R` — rerun current test
- `Esc` — quit

## Readiness

A test is not verified on a fixed frame count. Each case implements
`IsReady`, which waits for:

- `SceneSystem.Current` to be the expected scene
- the expected layer(s) to exist
- no `Uninitialised` tags left on that layer or its drawables
- the expected scene text to exist

`VisualTestMain` polls `IsReady` each frame and fails after 120 frames if the
ECS scene never settles.

## Current tests

- `main` — main menu text and initialized menu layer
- `song-select` — song select text and initialized menu layer
- `settings` — settings keybinds exist, are parented to the content layer, and
  that layer is above the background layer
- `play` — synthetic level, score/combo HUD, initialized play HUD layer

The settings case checks, for every keybind button:

- it is one of the seven expected actions
- its `Drawable.ZIndex` is the content layer
- its parent is the content layer, not the background
- it has a non-empty rectangle

That is the structural guard for the blank-settings regression.

## Adding a test

Implement `IVisualTest` in `VisualTestCases.cs` and add it to
`VisualTestCatalog.All`. `Begin` starts the scene, `IsReady` waits for ECS
initialization, and `Verify` returns a `VisualTestResult`.
