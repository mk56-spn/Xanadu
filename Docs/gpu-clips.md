# Clips on the GPU (proposed 2026-11-26, nothing built)

Clips have fallen behind the render graph. Outside the library, one shader uses them (`Game/hit_bar/shade.slang`); no
stack does. The timeline only knows what `Slang/SlangClips.cs` can read out of the text with regexes: explicit
`Clip` locals built from `clock`. Motion written any other way is invisible. `Lab/flat_meshes` is the test case: its
cards swing (`(k - 1) * 0.3 + sin(time + k) * 0.2`) and its strokes sway (`sin(time * 0.7 + k) * 0.25`), and the
timeline shows nothing.

**Goal (user's challenge):** clips live entirely on the GPU. The CPU only sets things up (plus a few tiny
peculiarities). The timeline catches everything that moves, whatever way the code writes it, and becomes a general
view of the shader's state over time.

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
