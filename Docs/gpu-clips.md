# Clips on the GPU (proposed 2026-11-26; slices 1 to 3 built the same day)

Clips have fallen behind the render graph. Outside the library, one shader uses them (`Game/hit_bar/shade.slang`); no
stack does. The timeline only knows what `Slang/SlangClips.cs` can read out of the text with regexes: explicit
`Clip` locals built from `clock`. Motion written any other way is invisible. `Lab/flat_meshes` is the test case: its
cards swing (`(k - 1) * 0.3 + sin(time + k) * 0.2`) and its strokes sway (`sin(time * 0.7 + k) * 0.25`), and the
timeline shows nothing.

**Goal (user's challenge):** clips live entirely on the GPU. The CPU only sets things up (plus a few tiny
peculiarities). The timeline catches everything that moves, whatever way the code writes it, and becomes a general
view of the shader's state over time.

## Where it stands (2026-11-26)

**Slice 1 is built, as two render graph features** (the user: every GPU-centric design goes through the render graph,
and what it lacks is added to it):

- **Probe nodes** (`Slang/ProbeNodes.cs`, a lowering beside `PresentDraws`, called from `CompileAsWritten`). A body
  declaring `uniform float4 xanaduProbe;` (from, span in seconds, beats a second) gets one generated `[Pass]` per draw,
  `xanaduProbe_<draw>`, called at the end of `graph()`, plus `[Shared] [Size(64 * 256)] List<float4> xanaduProbes` and
  `[Shared] Stored<uint> xanaduProbeLanes`. A thread is a (copy, sample): it sets `copy`, puts the clock at the window's
  start, finds the first of 8 triangles of its copy with an area (corners built as the draw's own vertex stage builds
  them, on a 1024 px square quad: `probeCorner`), then calls the draw's function on those three corners at its sample's
  time and writes `probeMotion` (the plane map from the start: offset in quads, turn, scale). Library side:
  `xanadu/probe.slang`. Only draws whose `copies:` the CPU can count from constants are probed; 64 lanes at most.
  The game never declares the uniform, so it never probes. The shader tool appends it for any stack with a graph
  (`EffectPreview.Compile`) and sets it each frame (`IEditorPreview.UseProbeWindow`); VALUES hides it.
- **`[Reported]` lists and stored values** (`Rendering/Passes/GraphReports.cs`): copied each frame, after the last draw,
  into a ring of 4 download buffers per material, and the slot copied 4 frames ago read into
  `CanvasMaterial.Reports` (`ReportedCount`). No fence, no wait: 4 frames is past SDL_GPU's frames in flight.
- **The lanes are a graph** (`Assets/Shaders/Editor/probe_lanes`): it declares the same shared list, so the shared
  ordering runs it after the preview. Three passes over the lanes: `measure` (low/high per channel, which channels
  move), `keep` (moving, and unlike every earlier lane: flat_meshes' four pages a thing fold into one lane), `rank`
  (rows in lane order); then a 1 × 1 draw shades each row's moving channels as curves (x cyan, y pink, turn gold, scale
  lime) and the playhead. Only `rows` (count, then lane | channels << 8) is `[Reported]`, for the row labels and the
  panel's height.
- **Host:** `ProbeLanes` (header with the key and window, labels from `ProbeNodes.Labels`, the curves item) under the
  clips in `TimelineColumn`, which took the clips' node and timeline out of `ShaderCodeEditor`. The window is the turn
  the clips show, else 8 s round the clock (`ClipTimeline.Shown`).
- **Checked:** `./run.sh shaders --open=Lab/flat_meshes`: six MOTION rows (shown 0, 4, 8, 12, 16, 20: the four cards'
  swing and the two strokes' sway), nothing written in the stack. Tests: `Slang/Tests/ProbeNodeTests.cs`.
- **Not yet:** draws counted on the GPU (`copies: pool`) and by values aren't probed; brick stacks (deform + shade
  without `graph()`) aren't either; the probe quad is a fixed square, not the item's; curves of one row overlap when
  their shapes match (a swing's x and turn); the probes run every frame rather than only when the window or the code
  changes; clip spans still come from `SlangClips` (slice 2).

**Slice 2 is built** (2026-11-26): clips report themselves and the regex scanner is gone.

- **Library** (`xanadu/anim.slang`): `public static bool probing`; while it is on, a clip reports itself into
  thread-private statics (`probedClips`, two float4 a clip: start, length, its clock's loop, easing; then beats /
  bounced / reversed flags, what its clock counts from, its site), once per distinct record, and `Keys.at(clip)`
  reports its places (`probedKeys`). A clip reports both when it is **read** (`linearAt`, so inline clips count) and
  when its **declaration completes** (`xanaduSited`): reading alone missed hit_bar, whose shade returns before any
  `progress` while the note isn't hit. `ISince` gained `sinceId` (a key's number, 1000 + an event's, a note 1008 =
  judged) and `Clock` carries it as `counts`, which is the old `Pressed`.
- **Sites** (`Slang/ClipProbes.cs`): the lowering wraps each `Clip name = …;` as `(…).xanaduSited(n)`, on the same
  line. The CPU reads the same declarations (`ClipProbes.Sites`) only to name rows and jump to their line: the one
  piece of text left.
- **The clip pass** (`xanaduProbe_clips`, 32 threads, run before the motion passes): thread 0 runs the brick shade on
  the probe quad's middle (`tracedInput`), thread 1 the deform, the rest a copy of a draw each (its vertex function on
  one corner); each copies what it saw into its region of `[Shared] List<float4> xanaduClips`. A stack without
  `graph()` gets one that runs the probe, so shade-only stacks are probed too.
- **Back to the CPU:** the lanes stack declares `xanaduClips` `[Shared] [Reported]`; `ClipProbes.Read` folds the
  regions into `SlangClip`s (copies starting at different times become one staggered clip). `ClipTimeline` draws them as
  before; `SlangClips` keeps only its easing, key and event tables, `SlangClip.Fixed` and "not fixed in the code" are
  gone (a probed clip always has numbers).
  - **Deviation from the plan:** this reads clip records back for the CPU timeline to draw, rather than only a count.
    They are small (32 × 49 float4) and change only with the code, and the timeline's window (loops' common length)
    and its key-press markers are CPU-side. Drawing spans in a shader too would mean moving both onto the GPU.
- **The preview** probes every stack it can: it compiles with the window uniform and falls back to an unprobed compile
  when that fails (a `[Grid]` stack can't have a graph). The editor's overlays are never probed.
- **Checked:** `Game/hit_bar` shows `life` and `narrowing`, "plays on each judged", as the scanner did; the new
  `Learn/tour_clips` shows `pop` (looped, outBack), `trip` (beats, bounced, keys at 0.5 and 1) and `ring` (staggered in a
  loop: one row, copies fading). Tests: `ProbeNodeTests` (sites, a shade-only stack's graph, reading a report);
  `SlangClipsTests` deleted with the scanner.
- **Not yet:** clips only inside a draw's shade (not its vertex function) or a pass aren't reported; per-pixel stagger
  shows only what the middle of the quad sees; a run where the window had focus caught stray keystrokes in the editor.

**Slice 3 is built** (2026-11-26): colour samples.

- **`Slang/ColourProbes.cs`:** a generated pass per shade (`xanaduColour_<fn>`): the stack's own `shade`, then each probed
  draw's shade that returns one colour (a lane per copy). A thread is (copy, sample, place): it puts the clock at its
  sample, builds the input at one of four places on the probe quad (`probeInput`) and writes the shade's colour into
  `[Shared] List<float4> xanaduProbeColours`; `xanaduColourLanes` counts the lanes. A stack with nothing to sample runs
  a one-thread pass zeroing the count, so the lanes never show a stack that's gone.
- **The places** are one per quarter, off the halves, thirds and quarters (0.37/0.29, 0.71/0.41, 0.29/0.69, 0.63/0.77):
  at exact thirds, `Learn/fire`'s six panels put every place on a gap and its colour row came out still.
- **The lanes stack** classifies colour lanes beside motion lanes (`moving`, `kept` and `rows` cover both, motion
  first): a colour lane shows when any place changes over time (not when places differ: a checker is still), unlike
  every colour lane before it, and draws as four strips, one a place. Rows carry a colour bit (16) so the CPU picks the
  label from `ColourProbes.Labels`.
- **Checked:** `Learn/fire` shows a `shade` row flickering with its noise; `Learn/tour_clips` a `shade` row where a
  ring passes a place; `Lab/flat_meshes` shows its six motion rows and no colour rows (its look doesn't change in time).
- **Not yet:** four places see only what passes them (a small shape elsewhere on the quad is missed); a draw's shade
  sees the probe quad's uv, not its mesh's (`i.world`, `drawnTriangle` are zero).

## Idea: probe, don't parse

The GPU doesn't need to understand the code. It runs the shader's own functions at other times and measures what
changes.

### 1. Motion probe (first slice)

- In the shader editor, the graph adds a **probe pass** for each draw. A thread is one (copy, sample time) pair.
- The thread sets `time` and `beat` itself. Both are already statics (`xanadu/stage.slang` `public static float time`),
  filled by the clock preamble, which is the same thing the `xanaduClock` scrub override in `SlangShaders.cs` uses. It
  sets the `copy` static, then calls the draw's own vertex function (`shown` in flat_meshes) on **three reference
  vertices** of that copy.
- From three points it recovers the copy's 2D transform at that time: **position, angle, scale**. Write them to a
  probe list: `[copy][sample] -> (offset, angle, scale)`.
- Mesh draws: a reference vertex is a vertex id the draw would read from the copy's page (`pool.corner`), so pick
  three non-collinear vertices of the page, e.g. corners of the grid. Item quads: the quad's corners.
- **flat_meshes, with nothing written for it**, should show six lanes: four cards swinging ±0.2 rad over about 6.3 s and
  two strokes swaying ±0.25 rad over about 9 s, each phased per copy.
- **A copy that doesn't move** over the window (all samples equal within epsilon) gets no lane. Classifying happens on
  the GPU too: a reduction over each lane gives min, max and "moves", and later period and phase.
- **Window:** the timeline's visible range × N samples (256 to start). Rerun only when the compile, the window or a
  tuned value changes, not every frame. Cost: about 6 copies × 256 × 3 = 5k vertex calls.

### 2. Lanes drawn by a shader

- The timeline lanes are drawn by a UI shader that reads the probe list directly, the way `Ui/board_grid` draws the
  board's dots (`UI/Systems/Ui/BoardDrawSystem.cs`). The probe list is never read back to draw.
- Lane kinds: an angle curve, a position path (x/y curves), a scale curve, and later a colour strip.
- The playhead and scrub stay as they are: scrubbing already sets the clock through `xanaduClock`.

### 3. Colour samples

The same probe calls the shade function at a few points in each copy's box (centre and corners) per sample time, which
gives a colour strip. This is the only way to catch motion that never reaches a vertex (a stripe sliding inside a
shade).

### 4. Clips report themselves; the regex scanner goes

- In probe mode, `Clip`'s constructor (`xanadu/anim.slang`) appends start, length, easing, bounced/reversed and clock
  (kind, loop, what it counts from) to a probe list.
- Construction order within a thread is fixed, so the append index is a stable clip id.
- Keys read at a clip report their places the same way.
- Then `SlangClips.Find`, its `Evaluator` and its regexes are deleted. The only text left is labels (below).
- Stagger falls out for free: different copies report different starts.

### 5. Recording ring for stateful stacks

- A stack with state (`Lab/particles`: pools, events, fields) can't be probed at other times: calling `shown` at
  t + 3 s still reads this frame's lists.
- For these the graph keeps a **recording ring** on the GPU: each frame the preamble appends the stack's `Stored<T>`
  values, counters, block live counts (`Pool.alive`) and event fires (`Events<T>`).
- These lanes are **recorded**: you can replay them but not scrub them, and the timeline marks them that way.

## What the CPU still does

- **At compile time:** adds the probe pass and the probe lists to the graph plan (the planner already does this for
  every pass: `StackBuild`, `GraphPlan`). Editor only: the game never gets a probe pass.
- **Labels:** copy names and clip names, and the code line a lane jumps to. The GPU only knows "copy 3" and "clip #2".
  Clip names map clip ids from construction order to declared names.
- **One lane count, read back asynchronously a frame late,** so the timeline column can size itself (`_clipsNode.Height`
  in `ShaderCodeEditor`).

## Catches

- **Side effects:** a vertex or shade function that writes lists (`InterlockedAdd`, `Events.add`, `Pool.take`) would
  run again inside the probe. Add a `probing` static that is true inside the probe pass, which library writers check,
  and/or refuse probing for draws whose functions write. Decide when building.
- **Functions reading graph state** give this frame's state at every sample time. That's correct for cached data
  (flat_meshes' `[Cached]` pages) and wrong for live state, which is what the recording ring is for.
- **Per-pixel clips with `staggered(index)`** from a pixel value: the probe only sees the sample points it shades.
- **The 8-bindings-per-pass limit (SDL_GPU):** the probe pass binds what the draw binds plus the probe list. Check that
  heavy draws still fit.

## Order

1. Motion probe + shader lanes on `Lab/flat_meshes`. Done when six swinging lanes appear with no clip code in the
   stack, and the only readback is the lane count. Build + one shot of the shader editor on flat_meshes.
2. Clips reporting themselves; delete the regex scanner. `hit_bar/shade.slang` must still show its clips. Keep the
   [[shader-clips-timeline]] rule: every anim change teaches the timeline, now through the probe instead of
   `SlangClips`.
3. Colour samples.
4. Recording ring on `Lab/particles`.

## Open questions for the user

- Lane look: curves (angle/position over time) vs spans (clips). Probably both on one timeline, with probed lanes under
  the clip spans.
- Does the probe pass run in the game's editor for levels' item shaders too, or only in the shader editor at first?
