# Visual tests

The visual tests are an osu!framework-inspired scene-test runner for the ECS
screens. Each test is a sequence of steps run against a live ECS store, so it can
assert on entities, input and layout the way the running game does.

It does **not** compare screenshots. It can save the window on demand so a human
or an agent can look at it — see [Capturing](#capturing).

## How it is built

- The sidebar is ECS UI, like the rest of the project: a title, `RUN ALL`, then
  two scroll views, one row per test and the current test's steps. A step is grey
  until it passes and turns green.
- It is a MoonWorks host exe (`VisualTestMain` derives from `HostApp`). The
  scene draws under a canvas item offset by the sidebar's width.
- Everything is authored at 1920x1080 and the host scales the whole surface
  as 16:9, so the harness never reads the window size. `--resolution=WxH`
  sets the window (1600x900 by default).

## Run

```bash
# Interactive browser
./run.sh visual-tests

# One test, exit with 0/1
./run.sh visual-tests --visual-test=settings --visual-test-exit

# All tests
./run.sh visual-tests --visual-test=all --visual-test-exit

# Save the window to a PNG once it settles, then exit
./run.sh visual-tests --visual-test=settings --visual-test-shot=/tmp/harness.png
```

`run.sh visual-tests` is `dotnet run --project Tests/VisualTests -- <args>`.

`--visual-test-full` drops the sidebar so the scene, and a shot of it, spans
the whole window; without it the stage starts 340 px in and its right edge is cut off.

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

`--visual-test-shot=<path>` reads the host's own frame texture, so it needs
neither the compositor nor the window to be visible or focused.

Each capture happens once, after 60 frames or as soon as a longer test
finishes, then the process exits 0 (or 1 if
the PNG could not be written). Tests write levels, songs and profiles into
their own user folder (`~/.local/share/xanadu-visual-tests`), never the game's.

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

`SceneTestCatalog.All` lists every test. The runner prints this list when given an unknown slug.

## NUnit integration

`NUnitIntegrationTests.cs` has one NUnit test per scene, so Rider shows a run
arrow beside each. Each launches the visual-test exe as a child process (one
window per test) and reads its result, so they skip unless asked:

```bash
XANADU_VISUAL_TESTS=1 dotnet test Tests/Xanadu.Tests.csproj --filter VisualSceneTests
```

## Adding a test

Add a `SceneTest` subclass to `SceneTestCatalog.All` and build its steps in
`SetUpSteps`. Use `Context.Entities` and `Context.Scenes` for live assertions.
`SceneTestRunnerTests` covers step ordering, assertion failure and timeout.
